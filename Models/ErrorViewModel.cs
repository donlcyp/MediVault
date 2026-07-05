namespace MediVault.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public int StatusCode { get; set; } = StatusCodes.Status500InternalServerError;

        public string Title => StatusCode switch
        {
            StatusCodes.Status404NotFound => "Page not found",
            StatusCodes.Status403Forbidden => "Access denied",
            _ => "Something went wrong"
        };

        public string Message => StatusCode switch
        {
            StatusCodes.Status404NotFound => "The page you requested could not be found.",
            StatusCodes.Status403Forbidden => "You do not have permission to view this resource.",
            _ => "An unexpected error occurred while processing your request."
        };

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
