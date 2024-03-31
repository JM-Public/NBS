using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("NBS")]
    public class NBSController : ControllerBase
    {
        private readonly IConfiguration configuration;
        private readonly IUserService userService;
        private readonly IDistributedCache redisCache;

        public NBSController(IConfiguration configuration, IUserService userService, IDistributedCache redisCache)
        {
            this.configuration = configuration;
            this.userService = userService;
            this.redisCache = redisCache;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Login([FromBody] AuthenticateRequest model)
        {
            var response = userService.Authenticate(model);

            if (response == null)
                return BadRequest(new { message = "Username or password is incorrect" });

            return Ok(response);
        }

        [HttpGet("users")]
        public IActionResult GetUsers()
        {
            var users = userService.GetAll();
            return Ok(users);
        }

        [HttpGet("search")]
        public IActionResult Search([FromBody] SearchRequest model)
        {
            // TODO: cache with latitude & longitude.
            // # Round up follow with radius parameter.
            // # seealso: https://gis.stackexchange.com/questions/8650/measuring-accuracy-of-latitude-and-longitude

            var cachedResponse = redisCache.GetString(model.keyword ?? string.Empty);

            SearchResponse responses;

            if (cachedResponse == null)
            {
                #region " Calling Google Place API "

                using (var client = new HttpClient())
                {
                    var requestUri = new StringBuilder();

                    requestUri.Append("https://maps.googleapis.com/maps/api/place/nearbysearch/json");
                    requestUri.Append($"?location={model.latitude},{model.longitude}");
                    requestUri.Append("&radius=1500");
                    requestUri.Append("&type=restaurant");
                    requestUri.Append($"&keyword={model.keyword}");
                    requestUri.Append($"&key={configuration["GooglePlacesSearchKey"]}");

                    var httpResponseMessage = client.PostAsync(requestUri.ToString(), new FormUrlEncodedContent(new Dictionary<string, string> { })).Result;

                    httpResponseMessage.EnsureSuccessStatusCode();

                    cachedResponse = httpResponseMessage.Content.ReadAsStringAsync().Result;
                }

                #endregion

                responses = JsonSerializer.Deserialize<SearchResponse>(cachedResponse, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                redisCache.SetString(model.keyword, cachedResponse, new DistributedCacheEntryOptions().SetAbsoluteExpiration(DateTimeOffset.Now.AddMinutes(1)));
            }
            else
            {
                responses = JsonSerializer.Deserialize<SearchResponse>(cachedResponse, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                responses.cached = true;
            }

            return Ok(responses);
        }
    }
}
