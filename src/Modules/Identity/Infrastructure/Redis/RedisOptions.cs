using System.ComponentModel.DataAnnotations;

namespace Zpi.Modules.Identity.Infrastructure.Redis;

public sealed class RedisOptions
{
    [Required] public string KeyPrefix { get; set; } = "zpi:identity:";
}
