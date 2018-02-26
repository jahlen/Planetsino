using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Planetsino.Models
{
    public class GamePlay
    {
        public const int WinScore = 25;
        public const int BetAmount = 20;
        public const int WinAmount = 40;

        public Player Player;
        public bool ComputersTurn = false;
        public bool GameOver = false;
        public int PotIncrease = 0;
        public int PlayerScoreIncrease = 0;
        public int ComputerScoreIncrease = 0;
        public string Message = "";
        public Random rand = new Random();

        public int[] PlayerCoins = new int[0];
        public int[] ComputerCoins = new int[0];
        public int PlayerScore => Player.PlayerScore;
        public int ComputerScore => Player.ComputerScore;
        public int Pot => Player.Pot;
        public int[] TossCoins() => new[] { rand.Next(2), rand.Next(2), rand.Next(2) };

        public async Task NewGame(Guid playerGuid, string playerClientName)
        {
            if (playerGuid == Guid.Empty)
                return;

            Player = await Player.Load(playerGuid, playerClientName);
            if (Player.Balance < BetAmount)
                return;

            Player.Balance -= BetAmount;
            Player.PlayerScore = 0;
            Player.ComputerScore = 0;
            Player.Pot = 0;
            await Player.Replace();
        }

        public async Task Play(Guid playerGuid, string playerClientName, string button)
        {
            if (playerGuid == Guid.Empty)
                return;

            Player = await Player.Load(playerGuid, playerClientName);

            switch (button.ToLower())
            {
                case "toss":
                    Toss();
                    break;
                case "call":
                    Call();
                    if (PlayerScore < WinScore)
                        PlayComputerTurn();
                    break;
                case "continue":
                    PlayComputerTurn();
                    break;
            }

            if (PlayerScore >= WinScore)
                Win();

            if (ComputerScore >= WinScore)
                Lose();

            await Player.Replace();
        }

        private void Toss()
        {
            PlayerCoins = TossCoins();
            int sum = PlayerCoins.Sum();
            if (sum == 0)
            {
                Player.Pot = 0;
                Message = "You lost the pot.";
                ComputersTurn = true;
            }
            else
            {
                Player.Pot += sum;
                //Message = $"Pot increased by {sum}";
                PotIncrease = sum;
            }
        }

        private void Call()
        {
            Player.PlayerScore += Pot;
            PlayerScoreIncrease = Pot;
            Message = $"You call. The pot, {Pot}, has been added to your score.<br/>";
            Player.Pot = 0;
            ComputersTurn = true;
        }

        private void PlayComputerTurn()
        {
            var pot = 0;
            var computerCoins = new List<int>();
            do
            {
                var coins = TossCoins();
                computerCoins.AddRange(coins);
                var sum = coins.Sum();
                if (sum == 0)
                {
                    pot = 0;
                    break;
                }

                pot += sum;
            } while (pot < 5); // Computer will toss again if pot is less than 5

            Message += $"The computer earned {pot}.";
            Player.ComputerScore += pot;
            ComputerScoreIncrease = pot;
            ComputerCoins = computerCoins.ToArray();
            ComputersTurn = false;
        }

        private void Win()
        {
            Message += "<br/>You win!";
            Player.Balance += WinAmount;
            ComputersTurn = false;
            GameOver = true;
        }

        private void Lose()
        {
            Message += "<br/>The computer wins!";
            GameOver = true;
        }
    }
}