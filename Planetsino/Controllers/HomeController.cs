﻿using Planetsino.Models;
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
            Player player;

            // Check if no PlayerGuid is available
            if (!playerGuid.HasValue && GetPlayerGuid() == Guid.Empty)
            {
                ViewBag.Message = "Welcome new player.";

                player = Player.New();
                await player.Create();

                SetPlayerGuid(player.PlayerGuid);
                SetPlayerClientName(player.ClientName);

                return View(player);
            }

            ViewBag.Message = "Welcome back player.";
            if (playerGuid.HasValue)
            {
                // Use playerGuid from request
                player = await Player.Load(playerGuid.Value);

                SetPlayerGuid(player.PlayerGuid);
                SetPlayerClientName(player.ClientName);
            }
            else
            {
                player = await Player.Load(GetPlayerGuid(), GetPlayerClientName());
            }

            return View(player);
        }

        [HttpPost]
        public async Task<ActionResult> Account(string button)
        {
            var stopWatch = Stopwatch.StartNew();
            var initialRequestCharge = DbHelper.RequestCharge;

            Player player = null;
            var playerGuid = GetPlayerGuid();
            if (playerGuid == Guid.Empty || string.IsNullOrEmpty(GetPlayerClientName()))
                return RedirectToAction("CookieProblem");

            switch (button)
            {
                case "start":
                    return RedirectToAction("Play");
                case "deposit":
                    // Both options work. Uncomment any of them.

                    // Option 1: Load, change and then save changes
                    //if (player == null)
                    //    player = await Player.Load(playerGuid, clientName);
                    //player.Balance += 100;
                    //await player.Upsert();

                    // Option 2: Use Stored Procedure to do the changes
                    player = await Player.AdjustBalance(playerGuid, GetPlayerClientName(), 100);

                    ViewBag.Message = "$100 added to balance.";
                    break;
                case "withdraw":
                    // Both options work. Uncomment any of them.

                    // Option 1: Load, change and then save changes
                    //if (player == null)
                    //    player = await Player.Load(playerGuid, clientName);
                    //player.Balance -= 100;
                    //await player.Replace();

                    // Option 2: Use Stored Procedure to do the changes
                    player = await Player.AdjustBalance(playerGuid, GetPlayerClientName(), -100);

                    ViewBag.Message = "$100 withdrawn from balance.";
                    break;
            }

            ViewBag.Metrics = $"Elapsed milliseconds: {stopWatch.ElapsedMilliseconds} <br/> Consumption: {DbHelper.RequestCharge - initialRequestCharge:f2}";
            return View(player);
        }

        [HttpGet]
        public async Task<ActionResult> Play()
        {
            var playerGuid = GetPlayerGuid();
            if (playerGuid == Guid.Empty)
                return RedirectToAction("CookieProblem");

            var player = await Player.Load(playerGuid, GetPlayerClientName());
            if (player.Balance < 20)
                return RedirectToAction("");

            player.Balance -= 20;
            player.PlayerScore = 0;
            player.ComputerScore = 0;
            player.Pot = 0;
            await player.Replace();

            return View(player);
        }

        [HttpPost]
        public async Task<ActionResult> Play(string button)
        {
            var playerGuid = GetPlayerGuid();
            if (playerGuid == Guid.Empty)
                return RedirectToAction("CookieProblem");

            var player = await Player.Load(playerGuid, GetPlayerClientName());
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
                    var coins = Toss();
                    var sum = coins.Sum();
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

            await player.Replace();

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
            try
            {
                await test.RunTests();
                ViewBag.Message = "Tests completed. Run the tests multiple times to get stable results.";
            }
            catch (Exception ex)
            {
                ViewBag.Message = $"<p>Tests failed.</p><p>{ex.GetType().Name}</p><p>{ex.Message}</p><p>{ex.StackTrace}</p>";
            }
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

        [HttpGet]
        public ActionResult CookieProblem()
        {
            return View();
        }

        private void SetCookie(string name, string value)
        {
            var cookie = new HttpCookie(name, value) { Expires = DateTime.MinValue };

            if (Response.Cookies.AllKeys.Contains(name))
                Response.SetCookie(cookie);
            else
                Response.AppendCookie(cookie);
        }

        private string GetCookie(string name)
        {
            if (Request.Cookies.AllKeys.Contains(name))
                return Request.Cookies[name].Value;
            else
                return null;
        }

        private Guid GetPlayerGuid()
        {
            var guidStr = GetCookie("PlayerGuid");
            if (guidStr != null && Guid.TryParse(guidStr, out var guid))
                return guid;

            return Guid.Empty;
        }

        private void SetPlayerGuid(Guid playerGuid)
        {
            SetCookie("PlayerGuid", playerGuid.ToString());
        }

        private string GetPlayerClientName()
        {
            return GetCookie("ClientName");
        }

        private void SetPlayerClientName(string clientName)
        {
            SetCookie("ClientName", clientName);
        }
    }
}