
namespace GreenTasty.Aqa.Config;

public static class TestSettings
{
    public static string FrontendUrl =>
        (Environment.GetEnvironmentVariable("AQA_FRONTEND_URL")
        ?? "https://dl8v07zbkza4a.cloudfront.net")
        .TrimEnd('/');

    public static string ApiUrl =>
        (Environment.GetEnvironmentVariable("AQA_API_URL")
        ?? "https://ob2ahveso3.execute-api.eu-north-1.amazonaws.com/develop")
        .TrimEnd('/');

    public static string OwnerEmail =>
        Environment.GetEnvironmentVariable("AQA_OWNER_EMAIL")
        ?? throw new InvalidOperationException(
            "AQA_OWNER_EMAIL is not configured.");

    public static string OwnerPassword =>
        Environment.GetEnvironmentVariable("AQA_OWNER_PASSWORD")
        ?? throw new InvalidOperationException(
            "AQA_OWNER_PASSWORD is not configured.");
}
