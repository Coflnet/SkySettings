
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coflnet.Sky.Settings.Models
{
    [DataContract]
    public class User
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }
        [DataMember(Name = "externalId")]
        [MaxLength(32)]
        public string ExternalId { get; set; }

        [DataMember(Name = "settings")]
        public HashSet<Setting> Settings { get; set; }
    }
}