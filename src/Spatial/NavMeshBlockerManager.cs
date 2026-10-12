using System;
using System.Collections.Generic;
using GTA.Math;
using GTA.Native;

namespace DynamicWorldEngine.Spatial
{
    public class NavMeshBlockerManager
    {
        private readonly Dictionary<string, List<int>> _environmentBlockers = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> _globalBlockerHandles = new List<int>();

        public int AddBlockingVolume(string environmentId, Vector3 centerPosition, Vector3 size, float heading)
        {
            try
            {
                // GTA V Native: ADD_NAVMESH_BLOCKING_OBJECT (0x387E77229B16CA9E)
                // Arguments: x, y, z, sizeX, sizeY, sizeZ, heading, bFlags, bFlags2
                int handle = Function.Call<int>(Hash.ADD_NAVMESH_BLOCKING_OBJECT, 
                    centerPosition.X, centerPosition.Y, centerPosition.Z,
                    size.X, size.Y, size.Z,
                    heading,
                    false, // flags
                    7      // All standard pedestrian/vehicle navmesh types
                );

                if (handle != 0)
                {
                    _globalBlockerHandles.Add(handle);
                    if (!string.IsNullOrEmpty(environmentId))
                    {
                        if (!_environmentBlockers.ContainsKey(environmentId))
                        {
                            _environmentBlockers[environmentId] = new List<int>();
                        }
                        _environmentBlockers[environmentId].Add(handle);
                    }
                }
                return handle;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NavMeshBlockerManager] Warning adding blocker: " + ex.Message);
                return 0;
            }
        }

        public int AddBlockingVolume(Vector3 centerPosition, Vector3 size, float heading)
        {
            return AddBlockingVolume("global", centerPosition, size, heading);
        }

        public void AddWallBlocker(string environmentId, Vector3 wallPos, float wallLength, float wallThickness, float wallHeight, float heading)
        {
            AddBlockingVolume(environmentId, wallPos, new Vector3(wallThickness, wallLength, wallHeight), heading);
        }

        public void AddWallBlocker(Vector3 wallPos, float wallLength, float wallThickness, float wallHeight, float heading)
        {
            AddBlockingVolume("global", wallPos, new Vector3(wallThickness, wallLength, wallHeight), heading);
        }

        public void RemoveBlocker(int handle)
        {
            if (handle != 0)
            {
                try
                {
                    Function.Call(Hash.REMOVE_NAVMESH_BLOCKING_OBJECT, handle);
                    _globalBlockerHandles.Remove(handle);

                    foreach (var list in _environmentBlockers.Values)
                    {
                        list.Remove(handle);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[NavMeshBlockerManager] Warning removing blocker: " + ex.Message);
                }
            }
        }

        public void RemoveBlockersForEnvironment(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return;

            List<int> handles;
            if (_environmentBlockers.TryGetValue(environmentId, out handles))
            {
                foreach (var handle in handles)
                {
                    try
                    {
                        Function.Call(Hash.REMOVE_NAVMESH_BLOCKING_OBJECT, handle);
                        _globalBlockerHandles.Remove(handle);
                    }
                    catch { }
                }
                _environmentBlockers.Remove(environmentId);
            }
        }

        public void ClearAllBlockers()
        {
            foreach (var handle in _globalBlockerHandles)
            {
                try
                {
                    Function.Call(Hash.REMOVE_NAVMESH_BLOCKING_OBJECT, handle);
                }
                catch { }
            }
            _globalBlockerHandles.Clear();
            _environmentBlockers.Clear();
        }
    }
}
