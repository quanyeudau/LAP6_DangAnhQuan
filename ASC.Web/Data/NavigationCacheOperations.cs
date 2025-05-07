using ASC.Web.Data;
using ASC.Web.Models;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

public class NavigationCacheOperations : INavigationCacheOperations
{
    private readonly IDistributedCache _cache;
    private readonly string NavigationCacheName = "NavigationCache";

    public NavigationCacheOperations(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task CreateNavigationCacheAsync()
    {
        await _cache.SetStringAsync(NavigationCacheName, File.ReadAllText("Navigation/Navigation.json"));
    }

    public async Task<NavigationMenu> GetNavigationCacheAsync()
    {
        return JsonConvert.DeserializeObject<NavigationMenu>(await _cache.GetStringAsync(NavigationCacheName));
    }
}
