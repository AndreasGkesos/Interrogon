using Interrogon.Core.Models;

namespace Interrogon.App.ViewModels;

public static class QuestionTypeDisplay
{
    public static string GetLabel(QuestionType type) => type switch
    {
        QuestionType.TheoryQA => "Theory Q&A",
        QuestionType.WorkExperience => "Work Experience",
        QuestionType.CodeReview => "Code Review",
        QuestionType.AlgorithmProblem => "Algorithms",
        QuestionType.SystemDesign => "System Design",
        _ => type.ToString()
    };

    public static string GetGlyph(QuestionType type) => type switch
    {
        QuestionType.TheoryQA => "",         // Library - general knowledge
        QuestionType.WorkExperience => "",   // Briefcase - professional experience
        QuestionType.CodeReview => "",       // Code braces - reviewing code
        QuestionType.AlgorithmProblem => "", // Puzzle piece - problem solving
        QuestionType.SystemDesign => "",     // Diagram - architecture/design
        _ => ""
    };

    public static readonly IReadOnlyList<QuestionType> All =
    [
        QuestionType.TheoryQA,
        QuestionType.WorkExperience,
        QuestionType.CodeReview,
        QuestionType.AlgorithmProblem,
        QuestionType.SystemDesign
    ];
}
