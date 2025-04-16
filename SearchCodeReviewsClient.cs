using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace CodeReviewAssistant;

public class SearchCodeReviewsClient
{
    private readonly SearchClient searchClient;

    public SearchCodeReviewsClient()
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        var searchEndpoint = config["AZURE_SEARCH_ENDPOINT"];
        var searchApiKey = config["AZURE_SEARCH_API_KEY"];
        var indexName = "code-reviews";

        this.searchClient = new SearchClient(new Uri(searchEndpoint), indexName, new AzureKeyCredential(searchApiKey));
    }

    public async Task StoreReviewAsync(string code, string review)
    {
        var document = new
        {
            id = Guid.NewGuid().ToString(),
            code = code,
            review = review,
            tags = new[] { "Code Smells", "Security Issues", "Best Practices", "Readability" }
        };

        await searchClient.UploadDocumentsAsync(new[] { document }).ConfigureAwait(false);
    }

    public async Task<string> GetSimilarReviewsAsync(string code)
    {
        var options = new SearchOptions
        {
            Size = 3,
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions
            {
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive),
                SemanticConfigurationName = "review-config",
            },
        };

        var results = await searchClient.SearchAsync<SearchDocument>(code, options).ConfigureAwait(false);
        return string.Join("\n\n", results.Value.GetResults().Select(r =>
        {
            var doc = r.Document;
            return doc.TryGetValue("review", out var review) ? review.ToString() : string.Empty;
        }));
    }
}
