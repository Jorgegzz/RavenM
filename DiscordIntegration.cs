using System;
using System.Collections;
using RavenM.DiscordGameSDK;
using Steamworks;
using UnityEngine;

namespace RavenM
{
    public class DiscordIntegration : MonoBehaviour
    {
        public static DiscordIntegration Instance;
        
        public Discord Discord;

        public long discordClientID = 1007054793220571247;

        public long startSessionTime;
        
        
        private void Start()
        {
            Discord = new Discord(discordClientID, (UInt64) CreateFlags.Default);
            Plugin.logger.LogWarning("Discord Instance created");
            startSessionTime = ((DateTimeOffset) DateTime.Now).ToUnixTimeSeconds();

            StartCoroutine(StartActivities());
        }
        
        IEnumerator StartActivities()
        {
            UpdateActivity(Discord, Activities.InitialActivity);
            yield return new WaitUntil(GameManager.IsInMainMenu);
            UpdateActivity(Discord, Activities.InMenu);
        }

        private float _timer;
        private string _gameMode = "Insert Game Mode";
        private void FixedUpdate()
        {
           Discord.RunCallbacks();

           _timer += Time.fixedDeltaTime;

           if (_timer > 5f)
           {
               if (GameManager.instance == null) { return; }

               if (LobbySystem.instance.InLobby && !GameManager.IsIngame())
               {
                   var dropdown = InstantActionMaps.instance.gameModeDropdown;
                   _gameMode = dropdown.options[dropdown.value].text;
                   int currentLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                   int currentLobbyMemberCap = SteamMatchmaking.GetLobbyMemberLimit(LobbySystem.instance.ActualLobbyID);
                   UpdateActivity(Discord, Activities.InLobby, _gameMode ,currentLobbyMembers, currentLobbyMemberCap);
               }

               if (GameManager.IsIngame())
               {
                   int currentLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                   int currentLobbyMemberCap = SteamMatchmaking.GetLobbyMemberLimit(LobbySystem.instance.ActualLobbyID);
                   UpdateActivity(Discord, Activities.InMultiPlayerGame, _gameMode ,currentLobbyMembers, currentLobbyMemberCap);
               }
               
               _timer = 0f;
           }
        }

        public void UpdateActivity(Discord discord, Activities activity, string gameMode = "None", int currentPlayers = 1, int maxPlayers = 2)
        {
            var activityManager = discord.GetActivityManager();
            var activityPresence = new Activity();
            
            switch (activity)
            {
                case Activities.InitialActivity:
                    activityPresence = new Activity()
                    {
                        State = "Just Started Playing",
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InMenu:
                    activityPresence = new Activity()
                    {
                        State = "Waiting In Menu",
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InLobby:
                    activityPresence = new Activity()
                    {
                        State = "Waiting In Lobby",
                        Details = $"Game Mode: {gameMode}",
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Party = {
                            Size = {
                                CurrentSize = currentPlayers,
                                MaxSize = maxPlayers,
                            },
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InMultiPlayerGame:
                    activityPresence = new Activity()
                    {
                        State = "Playing Multiplayer",
                        Details = $"Game Mode: {gameMode}",
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Party = {
                            Size = {
                                CurrentSize = currentPlayers,
                                MaxSize = maxPlayers,
                            },
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InSinglePlayerGame:
                    activityPresence = new Activity()
                    {
                        State = "Playing Singleplayer",
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                    
               
            }
            activityManager.UpdateActivity(activityPresence, result =>
            {
                Plugin.logger.LogInfo($"Update Discord Activity {result}");
            });
        }

        public enum Activities
        {
            InitialActivity,
            InMenu,
            InLobby,
            InMultiPlayerGame,
            InSinglePlayerGame,
        }
    }
}