using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;

namespace DynamicWorldEngine.Audio
{
    /// <summary>
    /// Autonomous 3D Spatial Audio & Music Streaming Manager.
    /// Manages native GTA V audio scenes (e.g. DLC_BATTLE_MIX1_CLUB_SCENE for nightclub acoustics)
    /// and controls venue static radio emitters across terraces, bars, and lounges.
    /// </summary>
    public class PartyAudioManager
    {
        private static readonly string[] MansionEmitters = new string[]
        {
            "se_dlc25-2_mansion_exterior_pool_bar_loc_2",
            "se_dlc25-2_mansion_exterior_terrace_loc_2",
            "dlc25-2_mansion_exterior_central_loc_2",
            "se_dlc_ba_club_front_door_left_loc_1",
            "se_dlc_ba_club_front_door_right_loc_1"
        };

        private readonly List<string> _activeEmitters = new List<string>();
        private string _activeAudioScene = string.Empty;
        private bool _isPlaying = false;

        public bool IsPlaying { get { return _isPlaying; } }

        /// <summary>
        /// Starts high-energy party acoustics and streams authentic radio station music through venue emitters.
        /// </summary>
        public void StartPartyAudio(string venueId = "", string venueName = "", string radioStation = "RADIO_22_DLC_BATTLE_MIX1_RADIO")
        {
            StopAudio();

            try
            {
                // 1. Activate After Hours Club Acoustic Scene for punchy spatial bass and club reverb
                string scene = "DLC_BATTLE_MIX1_CLUB_SCENE";
                Function.Call(Hash.START_AUDIO_SCENE, scene);
                _activeAudioScene = scene;

                // 2. Resolve target radio station
                string station = string.IsNullOrEmpty(radioStation) ? "RADIO_22_DLC_BATTLE_MIX1_RADIO" : radioStation;

                // 3. Enable Venue Static Radio Emitters
                foreach (string emitter in MansionEmitters)
                {
                    try
                    {
                        // Native Hash 0x399D2D3B33F1B8EB: SET_STATIC_EMITTER_ENABLED
                        Function.Call((Hash)0x399D2D3B33F1B8EB, emitter, true);

                        // Native Hash 0xACF57305B12AF907: SET_EMITTER_RADIO_STATION
                        Function.Call((Hash)0xACF57305B12AF907, emitter, station);

                        _activeEmitters.Add(emitter);
                    }
                    catch { }
                }

                _isPlaying = true;
                Console.WriteLine("[PartyAudioManager] Started 3D party audio: Scene='{0}', Station='{1}', Emitters={2}", scene, station, _activeEmitters.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PartyAudioManager] StartPartyAudio warning: " + ex.Message);
            }
        }

        /// <summary>
        /// Starts ambient, refined cultural audio for art galleries and museums (Kortz Center).
        /// </summary>
        public void StartGalleryAudio(string venueId = "", string venueName = "")
        {
            StopAudio();

            try
            {
                string station = "RADIO_09_INDIE"; // Ambient indie/alternative or Motown for art gallery
                foreach (string emitter in MansionEmitters)
                {
                    try
                    {
                        Function.Call((Hash)0x399D2D3B33F1B8EB, emitter, true);
                        Function.Call((Hash)0xACF57305B12AF907, emitter, station);
                        _activeEmitters.Add(emitter);
                    }
                    catch { }
                }

                _isPlaying = true;
                Console.WriteLine("[PartyAudioManager] Started Gallery ambient audio: Station='{0}'", station);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PartyAudioManager] StartGalleryAudio warning: " + ex.Message);
            }
        }

        /// <summary>
        /// Cleans up all active audio scenes and mutes static radio emitters.
        /// </summary>
        public void StopAudio()
        {
            if (!_isPlaying && string.IsNullOrEmpty(_activeAudioScene)) return;

            try
            {
                if (!string.IsNullOrEmpty(_activeAudioScene))
                {
                    Function.Call(Hash.STOP_AUDIO_SCENE, _activeAudioScene);
                    _activeAudioScene = string.Empty;
                }

                foreach (string emitter in _activeEmitters)
                {
                    try
                    {
                        Function.Call((Hash)0x399D2D3B33F1B8EB, emitter, false);
                    }
                    catch { }
                }
                _activeEmitters.Clear();

                _isPlaying = false;
                Console.WriteLine("[PartyAudioManager] Stopped audio scenes and silenced static emitters.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PartyAudioManager] StopAudio warning: " + ex.Message);
            }
        }
    }
}
