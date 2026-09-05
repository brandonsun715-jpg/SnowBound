using UnityEngine;
using SnowBound.Buildings;
using SnowBound.Lifts;
using SnowBound.Mountain;
using SnowBound.Player;
using SnowBound.Weather;

namespace SnowBound.Resort
{
    /// <summary>
    /// One person on the mountain.
    ///
    /// The behaviour is a plain sequence rather than any kind of AI, because
    /// what a resort needs is not clever guests, it is a mountain that is
    /// visibly being used: people crossing the base area, a queue at the
    /// lift, chairs with somebody in them, and figures coming down the run.
    ///
    /// They walk on the height field rather than on colliders. Forty
    /// CharacterControllers would cost more than the entire rest of the game
    /// and would look no different from a hundred metres up.
    /// </summary>
    public class Guest : MonoBehaviour, ILiftPassenger
    {
        public enum Activity
        {
            Arriving,
            AtLodge,
            WalkingToLift,
            Queueing,
            RidingLift,
            Descending,
            Leaving
        }

        [Header("Who they are")]
        public float money = 180f;
        [Range(0f, 1f)] public float happiness = 0.7f;
        [Tooltip("0 is a first timer, 1 skis anything.")]
        [Range(0f, 1f)] public float ability = 0.5f;
        public int preferredPiste;
        public LocomotionKind gear = LocomotionKind.Ski;
        public Activity activity = Activity.Arriving;

        [Header("Movement")]
        public float walkSpeed = 2.4f;
        public float turnSpeed = 420f;

        public int RunsCompleted { get; private set; }

        GuestDirector _director;
        MountainGenerator _mountain;
        Chairlift _lift;
        LodgeBuilder _lodge;
        WeatherSystem _weather;

        Vector3 _entrance;
        Vector3 _target;
        Transform _seat;
        Vector3 _seatOffset;
        Transform _skis, _board;

        float _wait;
        float _lateral;
        int _runsWanted = 3;

        public void Begin(GuestDirector director, Vector3 entrance, Transform skis, Transform board)
        {
            _director = director;
            _entrance = entrance;
            _skis = skis;
            _board = board;

            _mountain = MountainGenerator.Instance;
            _lift = Chairlift.Instance;
            _lodge = LodgeBuilder.Instance;
            _weather = WeatherSystem.Instance;

            _runsWanted = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, ability));
            _lateral = Random.Range(-0.55f, 0.55f);

            transform.position = entrance;
            ShowGear(false);
            Head(LodgePoint());
        }

        // ---------------- lift passenger -----------------------------------

        Transform ILiftPassenger.Transform { get { return transform; } }
        public LocomotionKind Gear { get { return gear; } }
        public bool WaitingToBoard { get { return activity == Activity.Queueing; } }

        public void BoardLift(Transform seat, Vector3 seatOffset)
        {
            _seat = seat;
            _seatOffset = seatOffset;
            activity = Activity.RidingLift;
        }

        public void LeaveLift(Vector3 position, Vector3 facing, Vector3 velocity)
        {
            _seat = null;
            transform.position = position;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

            activity = Activity.Descending;
            ShowGear(true);
        }

        void OnDestroy()
        {
            if (_lift != null) _lift.Unregister(this);
        }

        // ---------------- living ------------------------------------------

        void Update()
        {
            float dt = Time.deltaTime;

            if (activity == Activity.RidingLift)
            {
                if (_seat != null)
                    transform.SetPositionAndRotation(_seat.TransformPoint(_seatOffset), _seat.rotation);
                return;
            }

            switch (activity)
            {
                case Activity.Arriving: Walk(dt, Activity.AtLodge); break;
                case Activity.AtLodge: Rest(dt); break;
                case Activity.WalkingToLift: Walk(dt, Activity.Queueing); break;
                case Activity.Queueing: Queue(dt); break;
                case Activity.Descending: Descend(dt); break;
                case Activity.Leaving: Walk(dt, Activity.Leaving); break;
            }
        }

        void Head(Vector3 point)
        {
            _target = point;
        }

        /// <summary>Walk towards the target; hand on to the next thing on arrival.</summary>
        void Walk(float dt, Activity next)
        {
            Vector3 flat = _target - transform.position;
            flat.y = 0f;

            if (flat.magnitude < 2.5f)
            {
                Arrived(next);
                return;
            }

            Advance(flat.normalized, walkSpeed, dt);
        }

        void Arrived(Activity next)
        {
            switch (next)
            {
                case Activity.AtLodge:
                    activity = Activity.AtLodge;
                    _wait = Random.Range(3f, 9f);
                    break;

                case Activity.Queueing:
                    activity = Activity.Queueing;
                    if (_lift != null) _lift.Register(this);
                    _wait = 0f;
                    break;

                case Activity.Leaving:
                    if (_director != null) _director.Release(this);
                    break;
            }
        }

        void Rest(float dt)
        {
            _wait -= dt;
            if (_wait > 0f) return;

            if (_director != null) _director.SpendAtLodge(this);

            // Warm, fed, and ready to queue.
            happiness = Mathf.Clamp01(happiness + 0.04f);
            ShowGear(true);
            activity = Activity.WalkingToLift;
            Head(LiftPoint());
        }

        void Queue(float dt)
        {
            _wait += dt;

            // Standing in a lift queue is the least fun part of any resort.
            happiness = Mathf.Clamp01(happiness - dt * 0.006f);

            // Shuffle towards the loading point rather than standing rigid.
            Vector3 flat = LiftPoint() - transform.position;
            flat.y = 0f;
            if (flat.magnitude > 1.2f) Advance(flat.normalized, walkSpeed * 0.4f, dt);
            else Settle();
        }

        void Descend(float dt)
        {
            if (_mountain == null) { activity = Activity.Leaving; Head(_entrance); return; }

            float z = transform.position.z;

            if (z <= _director.finishZ)
            {
                RunsCompleted++;
                happiness = Mathf.Clamp01(happiness + 0.06f);
                if (_director != null) _director.SpendOnTheHill(this);

                bool doneForToday = RunsCompleted >= _runsWanted ||
                                    (_director != null && _director.LiftsClosing);

                if (doneForToday)
                {
                    ShowGear(false);
                    activity = Activity.Leaving;
                    Head(_entrance);
                }
                else
                {
                    activity = Activity.WalkingToLift;
                    Head(LiftPoint());
                }
                return;
            }

            // Follow the run's centre line, offset a little so a crowd of
            // guests spreads across the piste instead of forming a conga line.
            float ahead = Mathf.Max(0f, z - 14f);
            float half = _mountain.PisteHalfWidth(preferredPiste, ahead);
            Vector3 point = _mountain.PistePoint(preferredPiste, ahead);
            point.x += _lateral * half;

            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.back;

            float speed = Mathf.Lerp(7f, 17f, ability);
            Advance(flat.normalized, speed, dt);
        }

        void Advance(Vector3 direction, float speed, float dt)
        {
            Vector3 next = transform.position + direction * speed * dt;
            next.y = _mountain != null ? _mountain.SampleHeight(next.x, next.z) : next.y;
            transform.position = next;

            Quaternion want = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
        }

        void Settle()
        {
            Vector3 at = transform.position;
            at.y = _mountain != null ? _mountain.SampleHeight(at.x, at.z) : at.y;
            transform.position = at;
        }

        Vector3 LodgePoint()
        {
            if (_lodge == null) return _entrance;
            return _lodge.EntrancePosition + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-3f, 3f));
        }

        Vector3 LiftPoint()
        {
            if (_lift == null) return _entrance;
            return _lift.BoardingPoint + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
        }

        void ShowGear(bool wearing)
        {
            if (_skis != null) _skis.gameObject.SetActive(wearing && gear == LocomotionKind.Ski);
            if (_board != null) _board.gameObject.SetActive(wearing && gear == LocomotionKind.Snowboard);
        }
    }
}
