using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace DynamicWorldEngine.Spatial
{
    public enum InteriorJobState
    {
        Pending,
        RequestingIpl,
        WaitingForIpl,
        DiscoveringInterior,
        ActivatingEntitySets,
        RefreshingInterior,
        WaitingForInteriorReady,
        LoadingCollision,
        Teleporting,
        Completed,
        Failed
    }

    public class InteriorActivationJob
    {
        public InteriorDefinition Interior { get; private set; }
        public InteriorJobState State { get; private set; }
        public bool TeleportRequested { get; private set; }
        public string ErrorMessage { get; private set; }
        public int InteriorId { get; private set; }

        private int _iplIndex = 0;
        private int _frameAttempts = 0;
        private readonly TeleportPocketManager _pocketManager;
        private Action<InteriorActivationJob> _onCompleteCallback;

        public InteriorActivationJob(InteriorDefinition interior, TeleportPocketManager pocketManager, bool teleportPlayer = true, Action<InteriorActivationJob> onComplete = null)
        {
            Interior = interior;
            _pocketManager = pocketManager;
            TeleportRequested = teleportPlayer;
            _onCompleteCallback = onComplete;
            State = InteriorJobState.Pending;
        }

        public bool ProcessTick(Ped playerPed)
        {
            if (State == InteriorJobState.Completed || State == InteriorJobState.Failed)
            {
                return true;
            }

            try
            {
                switch (State)
                {
                    case InteriorJobState.Pending:
                        _iplIndex = 0;
                        _frameAttempts = 0;
                        State = (Interior.IplNames != null && Interior.IplNames.Count > 0) ? InteriorJobState.RequestingIpl : InteriorJobState.DiscoveringInterior;
                        break;

                    case InteriorJobState.RequestingIpl:
                        if (_iplIndex < Interior.IplNames.Count)
                        {
                            string ipl = Interior.IplNames[_iplIndex];
                            Function.Call(Hash.REQUEST_IPL, ipl);
                            State = InteriorJobState.WaitingForIpl;
                            _frameAttempts = 0;
                        }
                        else
                        {
                            State = InteriorJobState.DiscoveringInterior;
                        }
                        break;

                    case InteriorJobState.WaitingForIpl:
                        string curIpl = Interior.IplNames[_iplIndex];
                        if (Function.Call<bool>(Hash.IS_IPL_ACTIVE, curIpl) || _frameAttempts > 25)
                        {
                            _iplIndex++;
                            State = (_iplIndex < Interior.IplNames.Count) ? InteriorJobState.RequestingIpl : InteriorJobState.DiscoveringInterior;
                        }
                        else
                        {
                            _frameAttempts++;
                        }
                        break;

                    case InteriorJobState.DiscoveringInterior:
                        InteriorId = Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, 
                            Interior.InteriorCoords.X, Interior.InteriorCoords.Y, Interior.InteriorCoords.Z);

                        if (InteriorId != 0 && Function.Call<bool>(Hash.IS_VALID_INTERIOR, InteriorId))
                        {
                            State = InteriorJobState.ActivatingEntitySets;
                        }
                        else
                        {
                            State = InteriorJobState.LoadingCollision;
                        }
                        break;

                    case InteriorJobState.ActivatingEntitySets:
                        if (Interior.EntitySets != null)
                        {
                            foreach (var set in Interior.EntitySets)
                            {
                                Function.Call(Hash.ACTIVATE_INTERIOR_ENTITY_SET, InteriorId, set);
                            }
                        }
                        Function.Call(Hash.REFRESH_INTERIOR, InteriorId);
                        State = InteriorJobState.WaitingForInteriorReady;
                        _frameAttempts = 0;
                        break;

                    case InteriorJobState.WaitingForInteriorReady:
                        if (Function.Call<bool>(Hash.IS_INTERIOR_READY, InteriorId) || _frameAttempts > 30)
                        {
                            // Pin in memory strictly after entity sets and refresh (Rockstar Oracle)
                            if (InteriorId != 0)
                            {
                                Function.Call(Hash.PIN_INTERIOR_IN_MEMORY, InteriorId);
                            }
                            State = InteriorJobState.LoadingCollision;
                        }
                        else
                        {
                            _frameAttempts++;
                        }
                        break;

                    case InteriorJobState.LoadingCollision:
                        Function.Call(Hash.REQUEST_COLLISION_AT_COORD, 
                            Interior.InteriorCoords.X, Interior.InteriorCoords.Y, Interior.InteriorCoords.Z);

                        _pocketManager.RegisterPocket(
                            Interior.InteriorId,
                            Interior.InteriorId,
                            Interior.EntranceCoords,
                            Interior.InteriorCoords,
                            Interior.InteriorCoords,
                            Interior.Name
                        );

                        State = (TeleportRequested && playerPed != null && playerPed.Exists()) ? InteriorJobState.Teleporting : InteriorJobState.Completed;
                        break;

                    case InteriorJobState.Teleporting:
                        Screen.FadeOut(300);
                        Script.Wait(350);

                        playerPed.Position = Interior.InteriorCoords;
                        playerPed.Heading = Interior.InteriorHeading;
                        Script.Wait(100);

                        Screen.FadeIn(300);
                        State = InteriorJobState.Completed;
                        break;
                }

                if (State == InteriorJobState.Completed)
                {
                    Notification.PostTicker(string.Format("~g~[Spatial Planner]~w~~n~~y~{0}~w~ active!", Interior.Name), true);
                    if (_onCompleteCallback != null)
                    {
                        _onCompleteCallback(this);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                State = InteriorJobState.Failed;
                Console.WriteLine("[InteriorActivationJob] Error: " + ex.Message);
                return true;
            }

            return false;
        }
    }
}
