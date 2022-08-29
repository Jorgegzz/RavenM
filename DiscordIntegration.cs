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

        private LobbyManager _lobbyManager;
        private ActivityManager _activityManager;
        private void Start()
        {
            Discord = new Discord(discordClientID, (UInt64) CreateFlags.Default);
            Plugin.logger.LogWarning("Discord Instance created");
            startSessionTime = ((DateTimeOffset) DateTime.Now).ToUnixTimeSeconds();

            StartCoroutine(StartActivities());
            
            _lobbyManager = Discord.GetLobbyManager();
            _activityManager = Discord.GetActivityManager();
        }
        
        IEnumerator StartActivities()
        {
            UpdateActivity(Discord, Activities.InitialActivity);
            yield return new WaitUntil(GameManager.IsInMainMenu);
            UpdateActivity(Discord, Activities.InMenu);
        }
        
        // Private Variables that makes me question my coding skills
        private float _timer;
        private string _gameMode = "Insert Game Mode";
        private LobbyTransaction txn;
        private Lobby _currentLobby;
        private bool _isDiscordLobbyActive;
        private void FixedUpdate()
        {
           Discord.RunCallbacks();

           _timer += Time.fixedDeltaTime;

           if (_timer > 5f)
           {
               if (GameManager.instance == null) { return; }
               
               
               if (!LobbySystem.instance.InLobby && GameManager.IsIngame()) // Playing SinglePlayer
               {
                   var dropdown = InstantActionMaps.instance.gameModeDropdown;
                   _gameMode = dropdown.options[dropdown.value].text;
                   UpdateActivity(Discord, Activities.InSinglePlayerGame, _gameMode);
               }
               else if (LobbySystem.instance.InLobby)
               {
                   if (!_isDiscordLobbyActive)
                   {
                       txn = _lobbyManager.GetLobbyCreateTransaction();
                       txn.SetCapacity(16);
                       txn.SetType(LobbyType.Public); // TODO: Detect if the lobby is browsable or not to determine the LobbyType will be public or private
                   
                       _lobbyManager.CreateLobby(txn, (Result result, ref Lobby lobby) =>
                       {
                           _currentLobby = lobby;
                           Plugin.logger.LogInfo($"lobby {lobby.Id} created with secret {lobby.Secret}");
                       });
                       _isDiscordLobbyActive = true; // TODO: For now, there is no way the discord lobby destroys itself once you are out of a lobby
                   }
                   
                   if (!GameManager.IsIngame()) // Waiting In lobby Multiplayer
                   {
                       var dropdown = InstantActionMaps.instance.gameModeDropdown;
                       _gameMode = dropdown.options[dropdown.value].text;
                       int currentLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                       int currentLobbyMemberCap = SteamMatchmaking.GetLobbyMemberLimit(LobbySystem.instance.ActualLobbyID);
                       UpdateActivity(Discord, Activities.InLobby, _gameMode ,currentLobbyMembers, currentLobbyMemberCap);
                   }
                   else // Playing Multiplayer
                   {
                       int currentLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                       int currentLobbyMemberCap = SteamMatchmaking.GetLobbyMemberLimit(LobbySystem.instance.ActualLobbyID);
                       UpdateActivity(Discord, Activities.InMultiPlayerGame, _gameMode ,currentLobbyMembers, currentLobbyMemberCap);
                   }
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
                            Id = _currentLobby.Id.ToString(),
                            Size = {
                                CurrentSize = currentPlayers,
                                MaxSize = maxPlayers,
                            },
                        },
                        Secrets =
                        {
                            Match = "foo match secret", // TODO: I don't know how the discord secrets work for now,
                                                        // TODO: I just know that if I assign them a string, the invite /ask to join buttons appear
                                                        
                                                        // TODO: This Invite/Ask to Join Thing Should also be working in the MultiPlayerGame Activity-
                                                        // TODO: once I get this thing working in the Lobby Activity
                            Join= "abcd",
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