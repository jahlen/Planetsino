using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Threading.Tasks;

namespace Planetsino.Models
{
    public class Player : IDocument
    {
        public const string CollectionId = "players";
        public const string PartitionKey = "/partitionId";
        public const int InitialBalance = 200;

        [JsonProperty("id")]
        public Guid PlayerGuid;

        [JsonProperty("partitionId")]
        public string PartitionId
        {
            get
            {
                return GetPartitionId(PlayerGuid);
            }
        }

        [JsonProperty("balance")]
        public int Balance;

        [JsonProperty("playerScore")]
        public int PlayerScore;

        [JsonProperty("computerScore")]
        public int ComputerScore;

        [JsonProperty("pot")]
        public int Pot;

        [JsonProperty("lastUpdate")]
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime LastUpdate;

        [JsonIgnore]
        public string ClientName { get; set; }

        public static Player New()
        {
            return new Player { PlayerGuid = Guid.NewGuid(), Balance = InitialBalance, PlayerScore = 0, ComputerScore = 0, LastUpdate = DateTime.UtcNow };
        }

        public async Task Create()
        {
            await DbHelper.Create(this, CollectionId);
        }

        public async Task Upsert()
        {
            LastUpdate = DateTime.UtcNow;
            await DbHelper.Upsert(this, CollectionId);
        }

        public async Task Replace()
        {
            LastUpdate = DateTime.UtcNow;
            await DbHelper.Replace(this, PlayerGuid.ToString(), CollectionId);
        }

        public static async Task<Player> Load(Guid playerGuid, string clientName)
        {
            var docResponse = await DbHelper.Get<Player>(clientName, playerGuid.ToString(), GetPartitionId(playerGuid), CollectionId);
            return docResponse.Document;
        }

        public static async Task<Player> Load(Guid playerGuid)
        {
            var docResponse = await DbHelper.Get<Player>(playerGuid.ToString(), GetPartitionId(playerGuid), CollectionId);
            return docResponse.Document;
        }

        public static async Task<Player> AdjustBalance(Guid playerGuid, string clientName, int amount)
        {
            var str = await DbHelper.ExecStoredProcedure<string>(clientName, "adjustBalance", CollectionId, GetPartitionId(playerGuid), playerGuid, amount);
            var player = JsonConvert.DeserializeObject<Player>(str);
            player.ClientName = clientName; // Must copy this because it is not deserialized
            return player;
        }

        public static async Task<Player[]> SearchByBalance(int minBalance)
        {
            var filter = $"c.balance >= {minBalance}";
            var players = await DbHelper.Query<Player>(null, filter, Player.CollectionId);

            return players;
        }

        public static string GetPartitionId(Guid playerGuid)
        {
            return playerGuid.ToString();
        }
    }
}