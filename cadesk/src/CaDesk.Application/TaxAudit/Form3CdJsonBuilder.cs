using System.Text.Json.Nodes;
using CaDesk.Application.Tax;
using CaDesk.Domain.Clients;
using CaDesk.Domain.TaxAudit;

namespace CaDesk.Application.TaxAudit;

/// <summary>
/// Builds and validates CBDT Form 3CA-3CD / Form 3CB-3CD V2.5 JSON payloads from a tax audit assessment.
/// </summary>
public static class Form3CdJsonBuilder
{
    public static IncomeTaxJsonExportResult Build(
        Form3CdAssessment assessment,
        IReadOnlyList<Form3CdClauseResponse> clauses,
        Client? client,
        ExportForm3CdJsonRequest? opts = null)
    {
        var issues = new List<IncomeTaxJsonExportIssue>();
        var is3Cb = string.Equals(assessment.AuditType, "Form3CB", StringComparison.OrdinalIgnoreCase);

        var pan = CbdtJsonExportHelper.NormalizePan(
            !string.IsNullOrWhiteSpace(assessment.Pan) ? assessment.Pan : (client is not null ? CbdtJsonExportHelper.ExtractTaxId(client, "PAN") : ""),
            issues,
            is3Cb ? "$.FORM3CB.F3CB.PartA.PAN" : "$.FORM3CA.F3CA.PartA.PAN");

        var assesseeName = CbdtJsonExportHelper.Trunc(assessment.AssesseeName, 75, client?.Name ?? "Assessee");
        var assesseeCity = CbdtJsonExportHelper.Trunc(opts?.AssesseeCity, 50, "Kochi");
        var assesseeState = CbdtJsonExportHelper.ResolveStateCode(opts?.AssesseeStateCode, client);
        var assesseePin = CbdtJsonExportHelper.ResolvePinString(opts?.AssesseePinCode, "682001");
        var assesseeAddr1 = CbdtJsonExportHelper.Trunc(assessment.Address, 100, assesseeCity);

        var (_, ayDashed, fyStart, fyEnd) = CbdtJsonExportHelper.ParseAyWindow(assessment.FinancialYear, inputIsFy: true);
        var (ayStart4, _, _, _) = CbdtJsonExportHelper.ParseAyWindow(assessment.AssessmentYear, inputIsFy: false);
        var ayYearInt = int.TryParse(ayStart4, out var y4) ? Math.Clamp(y4, 2013, 2026) : 2026;
        var aySchemaDashed = ayYearInt is >= 2013 and <= 2026 ? $"{ayYearInt:D4}-{(ayYearInt + 1) % 100:D2}" : "2026-27";

        if (!string.Equals(aySchemaDashed, ayDashed, StringComparison.Ordinal))
        {
            issues.Add(new("warning", is3Cb ? "$.FORM3CB.F3CB.PartA.AssessmentYear" : "$.FORM3CA.F3CA.PartA.AssessmentYear",
                "3CD-AY-CLAMP",
                $"CBDT V2.5 schema supports AY 2013-14 through 2026-27; exported PartA.AssessmentYear as '{aySchemaDashed}'."));
        }

        var signDate = string.IsNullOrWhiteSpace(opts?.SigningDate) ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") : opts.SigningDate.Trim();
        var auditDate = string.IsNullOrWhiteSpace(opts?.AuditReportDate) ? fyEnd.ToString("yyyy-MM-dd") : opts.AuditReportDate.Trim();
        var membershipNo = CbdtJsonExportHelper.DigitsOnly(opts?.AuditorMembershipNo, 6);
        if (membershipNo.Length != 6) membershipNo = "123456";

        var auditorName = CbdtJsonExportHelper.Trunc(opts?.AuditorName, 75, "Statutory Tax Auditor");
        var caFirmName = CbdtJsonExportHelper.Trunc(opts?.CaFirmName, 125, "CA Firm & Associates");
        var auditorCity = CbdtJsonExportHelper.Trunc(opts?.AuditorCity, 50, assesseeCity);
        var auditorState = CbdtJsonExportHelper.ResolveStateCode(opts?.AuditorStateCode, client);
        var auditorPin = CbdtJsonExportHelper.ResolvePinString(opts?.AuditorPinCode, assesseePin);
        var auditorAddr1 = CbdtJsonExportHelper.Trunc(opts?.AuditorAddress, 100, auditorCity);
        var gstin = !string.IsNullOrWhiteSpace(assessment.Gstin)
            ? assessment.Gstin.Trim().ToUpperInvariant()
            : (client is not null ? CbdtJsonExportHelper.ExtractTaxId(client, "GSTIN") : "");

        var assesseeNameObj = new JsonObject { ["LastName"] = assesseeName };
        var assesseeAddrObj = new JsonObject
        {
            ["AddrDetail1"] = assesseeAddr1,
            ["CityOrTownOrDistrict"] = assesseeCity,
            ["StateCode"] = assesseeState,
            ["CountryCode"] = "91",
            ["PinCode"] = assesseePin,
        };

        var partA = new JsonObject
        {
            ["AssesseeName"] = assesseeNameObj.DeepClone(),
            ["AddressDetail"] = assesseeAddrObj.DeepClone(),
            ["PAN"] = pan,
            ["IndirectTaxFlag"] = string.IsNullOrWhiteSpace(gstin) ? "N" : "Y",
            ["Status"] = MapEntityStatusCode(assessment.Status),
            ["PartAStartDate"] = fyStart.ToString("yyyy-MM-dd"),
            ["PartAEndDate"] = fyEnd.ToString("yyyy-MM-dd"),
            ["AssessmentYear"] = aySchemaDashed,
        };
        if (!string.IsNullOrWhiteSpace(gstin))
        {
            partA["Form3cdIndirectTax"] = new JsonArray
            {
                new JsonObject
                {
                    ["IndirectTaxType"] = "GST",
                    ["StateCode"] = CbdtJsonExportHelper.ResolveStateCode(gstin[..2], client),
                    ["RegNo"] = CbdtJsonExportHelper.Trunc(gstin, 25),
                }
            };
        }

        var flags = new JsonObject
        {
            ["ChngMethodOfAcc"] = "N",
            ["ChngShareSec79"] = MapEntityStatusCode(assessment.Status) == 5 ? "N" : "X",
            ["IncomeCluaseixofsubsection2"] = "N",
            ["IncomeCluasexofsubsection2"] = "N",
            ["FurnishStatement"] = "N",
            ["SubSec2Sec286"] = "N",
        };

        var otherInfo1 = new JsonObject
        {
            ["LastName"] = auditorName,
            ["AddrDetail1"] = auditorAddr1,
            ["CityOrTownOrDistrict"] = auditorCity,
            ["StateCode"] = auditorState,
            ["PinCode"] = auditorPin,
            ["CountryCode"] = "91",
            ["Place"] = CbdtJsonExportHelper.Trunc(opts?.Place, 35, auditorCity),
            ["Date"] = signDate,
            ["MembershipNo"] = membershipNo,
        };
        if (!string.IsNullOrWhiteSpace(opts?.FirmRegNum))
            otherInfo1["FirmRegNum"] = CbdtJsonExportHelper.Trunc(opts.FirmRegNum, 8);

        var body = new JsonObject
        {
            ["CreationInfo"] = new JsonObject
            {
                ["SWVersionNo"] = CbdtJsonExportHelper.SoftwareVersion,
                ["SWCreatedBy"] = CbdtJsonExportHelper.SoftwareId,
                ["JSONCreatedBy"] = CbdtJsonExportHelper.SoftwareId,
                ["JSONCreationDate"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                ["IntermediaryCity"] = CbdtJsonExportHelper.Trunc(auditorCity, 50),
            },
            ["Form_Details"] = new JsonObject
            {
                ["FormName"] = is3Cb ? "FORM3CB" : "FORM3CA",
                ["Description"] = is3Cb ? "Form 3CB Tax Audit Report u/s 44AB" : "Form 3CA Tax Audit Report u/s 44AB",
                ["AssessmentYear"] = ayYearInt.ToString(),
                ["SchemaVer"] = "Ver2.5",
                ["FormVer"] = "1.0",
            },
            ["Declaration"] = is3Cb
                ? Build3CbDeclaration(assesseeNameObj, assesseeAddrObj, pan, auditDate, signDate, ayYearInt, fyStart, fyEnd, assesseeCity, opts?.NumberOfBranches ?? 0, clauses)
                : Build3CaDeclaration(assesseeNameObj, assesseeAddrObj, caFirmName, opts?.OtherLawActCode, auditDate, signDate, fyStart, fyEnd),
            ["PartA"] = partA,
            ["Form3cdFlags"] = flags,
            ["OtherInformation1"] = otherInfo1,
        };

        // Populate key quantified Form 3CD disallowance & GST breakup nodes
        AppendClauseData(body, assessment, clauses, assesseeCity, assesseeState, assesseePin, fyEnd);

        var formName = is3Cb ? "FORM3CB" : "FORM3CA";
        var schemaFile = is3Cb ? "schema_3CB.json" : "schema_3CA.json";
        var root = new JsonObject { [formName] = new JsonObject { [is3Cb ? "F3CB" : "F3CA"] = body } };
        var fileName = $"{pan}_{formName}_{assessment.AssessmentYear.Replace("-", "")}.json";

        return CbdtJsonExportHelper.ValidateAndFinalize(
            root,
            schemaFile,
            formName,
            "Ver2.5",
            assessment.AssessmentYear,
            assessment.FinancialYear,
            fileName,
            issues);
    }

    private static JsonObject Build3CaDeclaration(
        JsonObject assesseeNameObj, JsonObject assesseeAddrObj, string caFirmName,
        string? actCode, string auditDate, string signDate, DateOnly fyStart, DateOnly fyEnd) => new()
    {
        ["Point1"] = new JsonObject
        {
            ["I_We1"] = "We",
            ["AssesseeName"] = assesseeNameObj.DeepClone(),
            ["AddressDetail"] = assesseeAddrObj.DeepClone(),
            ["Me_Us_Ms"] = "Us",
            ["CA_FirmName"] = caFirmName,
            ["Act"] = string.IsNullOrWhiteSpace(actCode) ? "14" : actCode.Trim().PadLeft(2, '0'), // 14 = Companies Act, 2013
            ["I_We2"] = "We",
            ["My_Our_Their"] = "Our",
            ["AuditDated"] = auditDate,
            ["DateofsigningTaxAuditReport"] = signDate,
            ["PointA"] = new JsonObject
            {
                ["TypeOfAccount"] = "Profit and loss account",
                ["AuditStartDate"] = fyStart.ToString("yyyy-MM-dd"),
                ["AuditEndDate"] = fyEnd.ToString("yyyy-MM-dd"),
            },
            ["AuditYearDate"] = fyEnd.ToString("yyyy-MM-dd"),
            ["PointC"] = "Profit and loss account",
        },
        ["Point3"] = new JsonObject
        {
            ["My_Our1"] = "Our",
            ["My_Our2"] = "Our",
            ["Me_Us"] = "Us",
        }
    };

    private static JsonObject Build3CbDeclaration(
        JsonObject assesseeNameObj, JsonObject assesseeAddrObj, string pan,
        string auditDate, string signDate, int ayYearInt, DateOnly fyStart, DateOnly fyEnd,
        string hoCity, int branches, IReadOnlyList<Form3CdClauseResponse> clauses)
    {
        var obs = new JsonArray();
        foreach (var q in clauses.Where(c => c.Status == "Qualified" && !string.IsNullOrWhiteSpace(c.AuditorRemarks)))
        {
            obs.Add(new JsonObject
            {
                ["ObservationsCode"] = "17", // 17 - Others
                ["ObservationsVal"] = CbdtJsonExportHelper.Trunc($"Clause {q.ClauseNumber}: {q.AuditorRemarks}", 1000),
            });
        }

        var point5 = new JsonObject
        {
            ["My_Our1"] = "Our",
            ["My_Our2"] = "Our",
            ["Me_Us"] = "Us",
        };
        if (obs.Count > 0) point5["Observations"] = obs;

        return new JsonObject
        {
            ["Point1"] = new JsonObject
            {
                ["I_We"] = "We",
                ["DateofsigningTaxAuditReport"] = signDate,
                ["Year"] = ayYearInt,
                ["TypeOfAccount"] = "Profit and loss account",
                ["StartDate"] = fyStart.ToString("yyyy-MM-dd"),
                ["EndDate"] = fyEnd.ToString("yyyy-MM-dd"),
                ["AssesseeName"] = assesseeNameObj.DeepClone(),
                ["AddressDetail"] = assesseeAddrObj.DeepClone(),
                ["PAN"] = pan,
            },
            ["Point2"] = new JsonObject
            {
                ["I_We"] = "We",
                ["TypeOfAccount"] = "Profit and loss account",
                ["HeadOfficeLocation"] = CbdtJsonExportHelper.Trunc(hoCity, 150),
                ["NumberOfBrances"] = Math.Clamp(branches, 0, 999),
            },
            ["Point3"] = new JsonObject
            {
                ["PointB_A"] = new JsonObject { ["I_We"] = "We", ["My_Our"] = "Our" },
                ["PointB_B"] = new JsonObject { ["My_Our1"] = "Our", ["My_Our2"] = "Our" },
                ["PointB_C"] = new JsonObject
                {
                    ["My_Our1"] = "Our",
                    ["My_Our2"] = "Our",
                    ["Me_Us"] = "Us",
                    ["PointFirst"] = ayYearInt.ToString("D4"),
                    ["PointSecond"] = new JsonObject
                    {
                        ["typeOfAccount"] = "Profit and loss account",
                        ["PLSD"] = "Profit",
                    }
                }
            },
            ["Point5"] = point5,
        };
    }

    private static void AppendClauseData(
        JsonObject body,
        Form3CdAssessment a,
        IReadOnlyList<Form3CdClauseResponse> clauses,
        string city,
        string stateCode,
        string pin,
        DateOnly fyEnd)
    {
        var endStr = fyEnd.ToString("yyyy-MM-dd");

        if (a.TotalDisallowanceSec40a > 0m)
        {
            body["Form3cdAmtInadm40A"] = new JsonArray
            {
                new JsonObject
                {
                    ["DateOfPayment"] = endStr,
                    ["AmtOfPayment"] = CbdtJsonExportHelper.ToRupees(a.TotalDisallowanceSec40a),
                    ["NatureOfPayment"] = "Disallowance u/s 40(a)(ia) / non-deduction of TDS",
                    ["NameOfPayee"] = "Aggregated Schedule Party",
                    ["AddressDetails"] = new JsonObject
                    {
                        ["AddrDetail1"] = city,
                        ["CityOrTownOrDistrict"] = city,
                        ["StateCode"] = stateCode,
                        ["CountryCode"] = "91",
                        ["PinCode"] = pin,
                    }
                }
            };
        }

        if (a.TotalDisallowanceSec40A3 > 0m)
        {
            body["Form3cdIncomeUnderSec40A_3"] = new JsonArray
            {
                new JsonObject
                {
                    ["PaymntSec40A_3A"] = new JsonObject
                    {
                        ["DateOfPayment"] = endStr,
                        ["NatureOfPayment"] = "Cash payments exceeding Rs 10,000 u/s 40A(3)",
                        ["AmtOfPayment"] = CbdtJsonExportHelper.ToRupees(a.TotalDisallowanceSec40A3),
                        ["NameOfPayee"] = "Cash Payee Schedule",
                    }
                }
            };
        }

        if (a.TotalDisallowanceSec43B > 0m || a.TotalDisallowanceSec43Bh > 0m)
        {
            var arr43B = new JsonArray();
            if (a.TotalDisallowanceSec43B > 0m)
            {
                arr43B.Add(new JsonObject
                {
                    ["Section"] = "43B",
                    ["Description"] = "Statutory liabilities / interest / bonus unpaid before due date",
                    ["Amount"] = CbdtJsonExportHelper.ToRupees(a.TotalDisallowanceSec43B),
                });
            }
            if (a.TotalDisallowanceSec43Bh > 0m)
            {
                arr43B.Add(new JsonObject
                {
                    ["Section"] = "43Bh",
                    ["Description"] = "Sums payable to Micro & Small Enterprises beyond Sec 15 MSMED limit",
                    ["Amount"] = CbdtJsonExportHelper.ToRupees(a.TotalDisallowanceSec43Bh),
                });
            }
            body["Form3cdUnpaidStrySec43b"] = arr43B;
        }

        if (a.TotalPayments > 0m)
        {
            var totExp = CbdtJsonExportHelper.ToRupees(a.TotalPayments);
            body["Form3cdBreakUpGST"] = new JsonArray
            {
                new JsonObject
                {
                    ["TotAmtExp"] = totExp,
                    ["GoodOrSerGST"] = 0,
                    ["CompSchme"] = 0,
                    ["OthRegEntities"] = 0,
                    ["RegEntities"] = 0,
                    ["ExpNonRegGST"] = totExp,
                }
            };
        }
        _ = clauses;
    }

    private static int MapEntityStatusCode(string? status) => (status ?? "").Trim().ToLowerInvariant() switch
    {
        "individual" => 1,
        "huf" => 2,
        "firm" => 3,
        "llp" => 4,
        "company" => 5,
        "trust" => 6,
        "aop" => 7,
        "boi" => 12,
        _ => 5,
    };
}
