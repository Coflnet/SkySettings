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
        private readonly SettingsDbContext db;
        private readonly SettingsService service;

        /// <summary>
        /// Creates a new instance of <see cref="SettingsController"/>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="service"></param>
        public SettingsController(SettingsDbContext context, SettingsService service)
        {
            db = context;
            this.service = service;
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
        public async Task UpdateSetting(string userId, string settingKey, string newValue)
        {
            await service.UpdateSetting(userId,settingKey,newValue);
        }
        /// <summary>
        /// Updates multiple settings
        /// </summary>
        /// <param name="userId">the userId for the key</param>
        /// <param name="settings">the new settings</param>
        /// <returns></returns>
        [HttpPost]
        [Route("{userId}")]
        public async Task GetSetting(string userId, List<Setting> settings)
        {
            await service.UpdateSettings(userId,settings);
        }
        /// <summary>
        /// Retrieve a single setting value
        /// </summary>
        /// <param name="userId">the userId for the key</param>
        /// <param name="settingKey">the user specific key</param>
        /// <returns>The current value </returns>
        [HttpGet]
        [Route("{userId}/{settingKey}")]
        public async Task<string> GetSetting(string userId, string settingKey)
        {
            return await service.GetSetting(userId,settingKey);
        }
        /// <summary>
        /// retrieve multiple settings
        /// </summary>
        /// <param name="userId">the userId for the key</param>
        /// <param name="settingKeys">the user specific key</param>
        /// <returns>The current value </returns>
        [HttpGet]
        [Route("{userId}")]
        public async Task<IEnumerable<Setting>> GetSettings(string userId, [FromQuery(Name = "keys")] List<string> settingKeys)
        {
            return await service.GetSettings(userId,settingKeys);
        }
    }
}
