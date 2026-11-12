using System.ComponentModel.DataAnnotations;

namespace MAQ_API.Options;

public class HerepayOptions
{
    public const string SectionName = "Herepay";

    [Required, Url] public string BaseUrl { get; set; } = "";
    [Required] public string SecretKey { get; set; } = "";
    [Required] public string ApiKey { get; set; } = "";
    [Required] public string PrivateKey { get; set; } = "";
    [Required, RegularExpression("^https://.*", ErrorMessage = "CallbackUrl must start with https://")]
    public string CallbackUrl { get; set; } = "";
    [Required, RegularExpression("^https://.*", ErrorMessage = "RedirectUrl must start with https://")]
    public string RedirectUrl { get; set; } = "";
}
