using Microsoft.AspNetCore.Mvc;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;

namespace CodeReviewAssistant.Controllers;

[ApiController]
[Route("api/review")]
public class CodeReviewController : ControllerBase
{
    private readonly ChatClient chatClient;
    private readonly SearchCodeReviewsClient searchClient = new SearchCodeReviewsClient();
    private const string DeploymentName = "gpt-4o-mini";

    public CodeReviewController()
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        string endpoint = config["AZURE_OPENAI_ENDPOINT"];
        string apiKey = config["AZURE_OPENAI_APIKEY"];
        var openAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        this.chatClient = openAIClient.GetChatClient(DeploymentName);

    }

    [HttpPost]
    public async Task <IActionResult> AnalyzeCode([FromBody] CodeReviewRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
        {
            return BadRequest("Code input cannot be null or empty.");
        }

        var similarReviews = await searchClient.GetSimilarReviewsAsync(request.Code).ConfigureAwait(false);

        var prompt = $"""
            Analyze the following C# code and provide a structured review. Categorize the review into sections:
            - Code Smells
            - Security Issues
            - Best Practices
            - Readability

            {request.Code}

            Here are some similar code reviews for reference:
            {similarReviews}
            """;

        var message = new List<ChatMessage>
        {
            new UserChatMessage(prompt),
        };

        ChatCompletion response = await chatClient.CompleteChatAsync(message).ConfigureAwait(false);
        var reviewFeedback = response.Content[0].Text;

        await searchClient.StoreReviewAsync(request.Code, reviewFeedback).ConfigureAwait(false);

        var structuredFeedback = ParseReviewFeedback(reviewFeedback);
        return Ok(new { Feedback = reviewFeedback });
    }

    private static Dictionary<string, string> ParseReviewFeedback(string feedback)
    {
        var sections = new Dictionary<string, string>();
        var categories = new List<string> { "Code Smells", "Security Issues", "Best Practices", "Readability" };

        foreach (var category in categories)
        {
            var startIndex = feedback.IndexOf(category, StringComparison.OrdinalIgnoreCase);
            if (startIndex != -1)
            {
                var nextCategoryIndex = categories
                        .Select(c => feedback.IndexOf(c, startIndex + 1, StringComparison.OrdinalIgnoreCase))
                        .Where(i => i > startIndex)
                        .DefaultIfEmpty(feedback.Length)
                        .Min();

                sections[category] = feedback.Substring(startIndex, nextCategoryIndex - startIndex).Trim();
            }
        }

        return sections;
    }
}
