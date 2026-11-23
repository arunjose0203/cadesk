using CaDesk.Domain.Aqmm;

namespace CaDesk.Application.Aqmm;

public record AqmmAssessmentSummaryDto(
    Guid Id,
    string FinancialYear,
    string Status,
    int TotalScore,
    int MaxPossibleScore,
    decimal OverallPercentage,
    string MaturityLevel,
    int SectionAScore,
    int SectionBScore,
    int SectionCScore,
    decimal SectionAPercentage,
    decimal SectionBPercentage,
    decimal SectionCPercentage,
    int Level2Gap,
    int Level3Gap,
    string? LeadPartnerName,
    string? PartnerMembershipNo,
    string? FirmRegistrationNo,
    string? Udin,
    DateTime? CertifiedAt,
    int TotalQuestionsCount,
    int AnsweredQuestionsCount
);

public record AqmmQuestionResponseDto(
    Guid? ResponseId,
    string QuestionCode,
    string Section,
    string Title,
    string Description,
    int MaxPoints,
    int AwardedPoints,
    IReadOnlyList<AqmmScoringCriterion> Criteria,
    string? PartnerRemarks,
    string? EvidenceReference,
    bool IsAutoEvaluated,
    string? AutoEvaluationKey
);

public record UpdateAqmmResponseRequest(
    string QuestionCode,
    int AwardedPoints,
    string? PartnerRemarks,
    string? EvidenceReference
);

public record CertifyAqmmAssessmentRequest(
    string LeadPartnerName,
    string PartnerMembershipNo,
    string FirmRegistrationNo,
    string Udin
);

public record AqmmGapAnalysisDto(
    int CurrentScore,
    string CurrentLevel,
    int PointsNeededForLevel2,
    int PointsNeededForLevel3,
    IReadOnlyList<AqmmRecommendationDto> TopRecommendations
);

public record AqmmRecommendationDto(
    string QuestionCode,
    string Title,
    string Section,
    int CurrentPoints,
    int MaxPoints,
    int PotentialGain,
    string RemedialAction,
    string Priority // High, Medium, Low
);

public record AutoScoreResultDto(
    int TotalScoreBefore,
    int TotalScoreAfter,
    int AutoScoredCount,
    string MaturityLevel,
    string Message
);
