using System;
using System.Collections;
using RavenM.DiscordGameSDK;
using Steamworks;
using UnityEngine;

namespace RavenM
{
    public class DiscordIntegration : MonoBehaviour
    {
        public static DiscordIntegration instance;
        
        public Discord Discord;

        public long discordClientID = 1007054793220571247;

        public long startSessionTime;

        private ActivityManager _activityManager;
        private LobbyManager _lobbyManager;
        private void Start()
        {
            Discord = new Discord(discordClientID, (UInt64) CreateFlags.Default);
            Plugin.logger.LogWarning("Discord Instance created");
            startSessionTime = ((DateTimeOffset) DateTime.Now).ToUnixTimeSeconds();
            
            _activityManager = Discord.GetActivityManager();
            _lobbyManager = Discord.GetLobbyManager();
            
            StartCoroutine(StartActivities());
            
            _activityManager.OnActivityJoin += _ =>
            {
                var secret = LobbySystem.instance.ActualLobbyID + "_match";
                Plugin.logger.LogInfo($"OnJoin {secret}");
                _lobbyManager.ConnectLobbyWithActivitySecret(secret, (Result result, ref Lobby lobby) =>
                {
                    Console.WriteLine("Connected to lobby: {0}", lobby.Id);
                    _lobbyManager.ConnectNetwork(lobby.Id);
                    _lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
                    foreach (var user in _lobbyManager.GetMemberUsers(lobby.Id))
                    {
                        Plugin.logger.LogInfo($"lobby member: {user.Username}");
                    }
                });
            };
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
        private void FixedUpdate()
        {
           Discord.RunCallbacks();

           _timer += Time.fixedDeltaTime;

           if (_timer > 5f)
           {
                ChangeActivityDynamically();
                
               _timer = 0f;
           }
        }

        private bool _isInGame;
        private bool _isInLobby;
        private bool _isHost;
        private bool _isThereDiscordLobby;
        private Lobby _currentLobby;

        void ChangeActivityDynamically()
        {
            if (GameManager.instance == null) { return; }

            _isInGame = GameManager.instance.ingame;
            _isInLobby = LobbySystem.instance.InLobby;
            _isHost = LobbySystem.instance.IsLobbyOwner;


            if (_isInGame && !_isInLobby)
            {
                var dropdown = InstantActionMaps.instance.gameModeDropdown;
                _gameMode = dropdown.options[dropdown.value].text;
                UpdateActivity(Discord, Activities.InSinglePlayerGame, _gameMode);
            }
            else if (_isInLobby) 
            {
                int currentLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                int currentLobbyMemberCap = SteamMatchmaking.GetLobbyMemberLimit(LobbySystem.instance.ActualLobbyID);

                if (!_isInGame) // Waiting in Lobby
                {
                    var dropdown = InstantActionMaps.instance.gameModeDropdown;
                    _gameMode = dropdown.options[dropdown.value].text;
                    UpdateActivity(Discord, Activities.InLobby, _gameMode ,currentLobbyMembers, currentLobbyMemberCap, LobbySystem.instance.ActualLobbyID.ToString());
                }
                else // Playing in a Lobby
                {
                    UpdateActivity(Discord, Activities.InMultiPlayerGame, _gameMode ,currentLobbyMembers, currentLobbyMemberCap);
                }

                if (_isHost && !_isThereDiscordLobby) // Created a RavenM Lobby
                {
                    // Create a Discord Lobby to keep track of discord members
                    var transaction = _lobbyManager.GetLobbyCreateTransaction();
                    transaction.SetCapacity((uint)currentLobbyMemberCap);
                    transaction.SetType(LobbySystem.instance.PrivateLobby ? LobbyType.Private : LobbyType.Public);

                    _lobbyManager.CreateLobby(transaction, (Result result, ref Lobby lobby) =>
                    {
                        _currentLobby = lobby;
                        if (result != Result.Ok)
                        {
                            return;
                        }
                        
                        Plugin.logger.LogInfo($"lobby {lobby.Id} with capacity {lobby.Capacity}");
                        
                        foreach (var user in _lobbyManager.GetMemberUsers(lobby.Id))
                        {
                            Plugin.logger.LogInfo($"lobby member: {user.Username}");
                        }
                    });

                    _isThereDiscordLobby = true;
                }
            }
            else // Left the lobby
            {
                _lobbyManager.DisconnectLobby(_currentLobby.Id, (result) =>
                {
                    Plugin.logger.LogInfo($"Discord lobby disconnect, Result: {result}");
                });
                UpdateActivity(Discord, Activities.InMenu);
                _isThereDiscordLobby = false;
            }
        }
        
        public void UpdateActivity(Discord discord, Activities activity, string gameMode = "None", int currentPlayers = 1, int maxPlayers = 2, string lobbyID = "None")
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
                            Id = lobbyID,
                            Size = {
                                CurrentSize = currentPlayers,
                                MaxSize = maxPlayers,
                            },
                        },
                        Secrets =
                        {
                            Match = lobbyID + "_match",
                            Join = lobbyID + "_join",
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