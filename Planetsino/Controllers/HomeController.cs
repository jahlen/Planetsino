using Planetsino.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Planetsino.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            return View();
        }

        [HttpGet]
        public async Task<ActionResult> Account(Guid? playerGuid)
        {
            if (!playerGuid.HasValue)
            {
                ViewBag.Message = "Welcome new player.";

                var player = Player.New();
                player.PlayerGuid = Guid.Empty;
                return View(player);
            }
            else
            {
                ViewBag.Message = "Welcome back player.";

                var player = await Player.Load(playerGuid.Value);
                return View(player);
            }
        }

        [HttpPost]
        public async Task<ActionResult> Account(Guid playerGuid, string clientName, string button)
        {
            var stopWatch = Stopwatch.StartNew();
            var initialRequestCharge = DbHelper.RequestCharge;

            Player player = null;
            if (playerGuid == Guid.Empty)
            {
                player = Player.New();
                await player.Create();
                playerGuid = player.PlayerGuid;
                clientName = player.ClientName;
                ModelState.Clear(); // To make sure all controls on the page are updated with the new player
            }

            switch (button)
            {
                case "start":
                    if (player.Balance >= 20)
                    {
                        return RedirectToAction("Play", new { player.PlayerGuid });
                    }
                    break;
                case "deposit":
                    // Option 1: Load, change and then save changes
                    //if (player == null)
                    //    player = await Player.Load(playerGuid, clientName);
                    //player.Balance += 100;
                    //await player.Upsert();

                    // Option 2: Use Stored Procedure to do the changes
                    player = await Player.AdjustBalance(playerGuid, clientName, 100);

                    ViewBag.Message = "$100 added to balance.";
                    break;
                case "withdraw":
                    // Option 1: Load, change and then save changes
                    //if (player == null)
                    //    player = await Player.Load(playerGuid, clientName);
                    //player.Balance -= 100;
                    //await player.Replace();

                    // Option 2: Use Stored Procedure to do the changes
                    player = await Player.AdjustBalance(playerGuid, clientName, -100);

                    ViewBag.Message = "$100 withdrawn from balance.";
                    break;
            }

            ViewBag.Metrics = $"Elapsed milliseconds: {stopWatch.ElapsedMilliseconds} <br/> Consumption: {DbHelper.RequestCharge - initialRequestCharge:f2}";
            return View(player);
        }

        [HttpGet]
        public async Task<ActionResult> Play(Guid? playerGuid)
        {
            if (!playerGuid.HasValue)
                return RedirectToAction("");

            var player = await Player.Load(playerGuid.Value);
            if (player.Balance < 20)
                return RedirectToAction("");

            player.Balance -= 20;
            player.PlayerScore = 0;
            player.ComputerScore = 0;
            player.Pot = 0;
            await player.Upsert();

            return View(player);
        }

        [HttpPost]
        public async Task<ActionResult> Play(Guid playerGuid, string clientName, string button)
        {
            var player = await Player.Load(playerGuid, clientName);
            var computersTurn = false;
            var gameOver = false;
            var rand = new Random();
            int[] Toss() => new[] { rand.Next(2), rand.Next(2), rand.Next(2) };

            switch (button.ToLower())
            {
                case "toss":
                    var coins = Toss();
                    int sum = coins.Sum();
                    ViewBag.Coins = coins;
                    if (sum == 0)
                    {
                        player.Pot = 0;
                        ViewBag.Message = "You lost the pot.";
                        computersTurn = true;
                    }
                    else
                    {
                        player.Pot += sum;
                        ViewBag.Message = $"Pot increased by {sum}";
                    }
                    break;
                case "call":
                    if (player.Pot == 0)
                    {
                        ViewBag.Message = "You cannot Call when the pot is empty.";
                        return View(player);
                    }

                    player.PlayerScore += player.Pot;
                    ViewBag.Message = $"You call. The pot, {player.Pot}, has been added to your score.";
                    player.Pot = 0;
                    computersTurn = true;
                    break;
            }

            if (player.PlayerScore >= 25)
            {
                ViewBag.Message += "<br/>You win!";
                player.Balance += 40;
                computersTurn = false;
                gameOver = true;
            }

            if (computersTurn)
            {
                var pot = 0;
                do
                {
                    var sum = Toss().Sum();
                    if (sum == 0)
                    {
                        pot = 0;
                        break;
                    }

                    pot += sum;
                } while (pot < 4); // Computer will toss again if pot is less than 4

                ViewBag.Message += $"<br/>The computer earned {pot}.";
                player.ComputerScore += pot;
            }

            if (player.ComputerScore >= 25)
            {
                ViewBag.Message += "<br/>The computer wins!";
                gameOver = true;
            }

            await player.Upsert();

            ViewBag.GameOver = gameOver;

            return View(player);
        }

        [HttpGet]
        public ActionResult Admin()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Admin(int minBalance)
        {
            var stopWatch = Stopwatch.StartNew();
            var initialRequestCharge = DbHelper.RequestCharge;

            var players = await Player.SearchByBalance(minBalance);
            if (players.Length == 0)
                ViewBag.Message = "No accounts found.<br/>";
            else
                ViewBag.Message = "";

            ViewBag.Metrics = $"Elapsed milliseconds: {stopWatch.ElapsedMilliseconds} <br/> Consumption: {DbHelper.RequestCharge - initialRequestCharge:f2}";
            return View(players);
        }

        [HttpGet]
        public ActionResult PerformanceTest()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> PerformanceTest(PerformanceTest test)
        {
            await test.RunTests();
            ViewBag.Message = "Tests completed. Run the tests multiple times to get stable results.";
            return View(test);
        }

        [HttpGet]
        public ActionResult Diagnostics()
        {
            var diagnostics = new Diagnostics();
            diagnostics.Results = DbHelper.Diagnostics();
            return View(diagnostics);
        }

        [HttpPost]
        public async Task<ActionResult> Diagnostics(string button)
        {
            if (button == "delete")
            {
                await DbHelper.DeleteDatabases();
                ViewBag.Message = "Databases deleted!";
            }

            return Diagnostics();
        }
    }
}