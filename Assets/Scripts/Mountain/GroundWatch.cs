using UnityEngine;

namespace SnowBound.Mountain
{
    /// <summary>
    /// Keeps a built thing standing on the ground.
    ///
    /// Everything in the resort is placed by asking the mountain how high it
    /// is and then building at that height. The moment anything moves the
    /// mountain afterwards — the lodge flattening its pad, a run being carved,
    /// the player sculpting, a regenerate — every one of those heights is
    /// stale, and the building it belongs to is left hanging in the air or
    /// buried. Nothing was watching for that, which is why the lift's legs
    /// stopped reaching the snow.
    ///
    /// This watches. It remembers the ground it was built on, listens for the
    /// terrain saying it changed, and asks its owner to build again only when
    /// the ground under that owner has actually moved.
    ///
    /// That last part is what stops it looping. A lodge rebuild flattens its
    /// own pad, which raises another change; the second time round the ground
    /// is already flat, nothing has moved, and it stops.
    /// </summary>
    public class GroundWatch
    {
        /// <summary>Ignore anything smaller than this. Sculpting is continuous.</summary>
        public float tolerance = 0.05f;

        /// <summary>
        /// Seconds of stillness before rebuilding. A brush stroke changes the
        /// ground every frame it is held; without this, everything standing on
        /// it would be rebuilt every frame of the stroke instead of once at
        /// the end of it.
        /// </summary>
        public float quiet = 0.25f;

        MountainGenerator _mountain;
        System.Action _rebuild;

        Rect _footprint;
        float[] _samples;
        int _side;
        bool _dirty;
        bool _watching;
        float _changedAt;

        /// <summary>Start listening. Safe to call every enable.</summary>
        public void Follow(MountainGenerator mountain, System.Action rebuild)
        {
            Stop();

            _mountain = mountain;
            _rebuild = rebuild;

            if (_mountain == null || _rebuild == null) return;

            _mountain.TerrainChanged += OnTerrainChanged;
            _watching = true;
        }

        public void Stop()
        {
            if (_watching && _mountain != null) _mountain.TerrainChanged -= OnTerrainChanged;

            _watching = false;
            _dirty = false;
        }

        /// <summary>
        /// Record the ground this thing was just built on. Call it at the end
        /// of a build, with the patch of mountain the build stands on.
        /// </summary>
        public void Note(Rect footprint, int samplesPerSide = 5)
        {
            _footprint = footprint;
            _side = Mathf.Clamp(samplesPerSide, 2, 32);
            _samples = Read();
            _dirty = false;
        }

        /// <summary>Same, for something round.</summary>
        public void Note(Vector3 centre, float radius, int samplesPerSide = 5)
        {
            Note(Rect.MinMaxRect(centre.x - radius, centre.z - radius,
                                 centre.x + radius, centre.z + radius), samplesPerSide);
        }

        /// <summary>
        /// Call once a frame. Rebuilds at most once, a frame after the change,
        /// which is also what keeps a build that moves the ground from
        /// re-entering itself.
        /// </summary>
        public void Tick()
        {
            if (!_dirty) return;
            if (Time.unscaledTime - _changedAt < quiet) return;

            _dirty = false;

            if (!Moved()) return;

            System.Action rebuild = _rebuild;
            if (rebuild != null) rebuild();
        }

        void OnTerrainChanged(Rect changed)
        {
            if (_samples == null) return;
            if (!changed.Overlaps(_footprint, true)) return;

            _dirty = true;
            _changedAt = Time.unscaledTime;
        }

        bool Moved()
        {
            float[] now = Read();
            if (now == null || _samples == null || now.Length != _samples.Length) return false;

            for (int i = 0; i < now.Length; i++)
                if (Mathf.Abs(now[i] - _samples[i]) > tolerance) return true;

            return false;
        }

        float[] Read()
        {
            if (_mountain == null || !_mountain.Ready || _side < 2) return null;

            var heights = new float[_side * _side];

            for (int iz = 0; iz < _side; iz++)
            {
                float z = Mathf.Lerp(_footprint.yMin, _footprint.yMax, iz / (float)(_side - 1));

                for (int ix = 0; ix < _side; ix++)
                {
                    float x = Mathf.Lerp(_footprint.xMin, _footprint.xMax, ix / (float)(_side - 1));
                    heights[iz * _side + ix] = _mountain.SampleHeight(x, z);
                }
            }

            return heights;
        }
    }
}
