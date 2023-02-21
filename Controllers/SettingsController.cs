using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Settings.Models;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Collections.Generic;
using Coflnet.Sky.Settings.Services;

namespace Coflnet.Sky.Settings.Controllers
{
    /// <summary>
    /// Main Controller handling tracking
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly IEnumerable<ISettingsService> services;
        private readonly ILogger<SettingsController> logger;

        /// <summary>
        /// Creates a new instance of <see cref="SettingsController"/>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="service"></param>
        public SettingsController(IEnumerable<ISettingsService> services, ILogger<SettingsController> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        /// <summary>
        /// Updates a setting
        /// </summary>
        /// <param name="userId">the userId for the key</param>
        /// <param name="settingKey">the user specific key</param>
        /// <param name="newValue">the new value of the setting</param>
        /// <returns></returns>
        [HttpPost]
        [Route("{userId}/{settingKey}")]
        public async Task UpdateSetting(string userId, string settingKey, [FromBody] string newValue)
        {
            logger.LogInformation($"Updating {settingKey} for user {userId}");
            foreach (var service in services)
            {
                await service.UpdateSetting(userId, settingKey, newValue);
            }
        }
        /// <summary>
        /// Updates multiple settings
        /// </summary>
        /// <param name="userId">the userId for the key</param>
        /// <param name="settings">the new settings</param>
        /// <returns></returns>
        [HttpPost]
        [Route("{userId}")]
        public async Task UpdateSettings(string userId, [FromBody] List<Setting> settings)
        {
            foreach (var service in services)
            {
                await service.UpdateSettings(userId, settings);
            }
        }
        /// <summary>
        /// Retrieve a single setting value
        /// </summary>
        /// <param name="userId">the userId for the key</param>
        /// <param name="settingKey">the user specific key</param>
        /// <returns>The current value </returns>
        [HttpGet]
        [Route("{userId}/{settingKey}")]
        [ResponseCache(Duration = 3, Location = ResponseCacheLocation.Any, NoStore = false, VaryByQueryKeys = new string[] { "*" })]
        public async Task<string> GetSetting(string userId, string settingKey)
        {
            foreach (var service in services)
            {
                try
                {
                    return await service.GetSetting(userId, settingKey);
                }
                catch (System.Exception e)
                {
                    logger.LogError(e, $"Failed to load setting {settingKey} for user {userId}");
                }
            }
            throw new Coflnet.Sky.Core.CoflnetException("setting_not_found", "Failed to load settings");
        }
        /// <summary>
        /// retrieve multiple settings
        /// </summary>
        /// <param name="userId">the userId for the key</param>
        /// <param name="settingKeys">the user specific key</param>
        /// <returns>The current value </returns>
        [HttpGet]
        [Route("{userId}")]
        [ResponseCache(Duration = 10, Location = ResponseCacheLocation.Any, NoStore = false, VaryByQueryKeys = new string[] { "*" })]
        public async Task<IEnumerable<Setting>> GetSettings(string userId, [FromQuery(Name = "keys")] List<string> settingKeys)
        {
            foreach (var service in services)
            {
                return await service.GetSettings(userId, settingKeys);
            }
            throw new Coflnet.Sky.Core.CoflnetException("setting_not_found", "Failed to load settings");
        }
    }
}
