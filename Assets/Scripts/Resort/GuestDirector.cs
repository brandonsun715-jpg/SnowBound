using System.Collections.Generic;
using UnityEngine;
using SnowBound.Buildings;
using SnowBound.Core;
using SnowBound.Mountain;
using SnowBound.Player;

namespace SnowBound.Resort
{
    /// <summary>
    /// Turns arrivals into people.
    ///
    /// The demand model already says how many guests show up and when. This
    /// gives a capped number of them a body and lets the rest be counted
    /// without being simulated: a resort with two hundred visitors a day only
    /// ever needs forty of them on screen for the mountain to look busy, and
    /// the other hundred and sixty still pay.
    ///
    /// It is also where a guest's money reaches the ledger, so a guest never
    /// needs to know what a ledger is.
    /// </summary>
    public class GuestDirector : MonoBehaviour
    {
        public ResortTraffic traffic;
        public Ledger ledger;
        public ResortClock clock;
        public MountainGenerator mountain;
        public LodgeBuilder lodge;

        [Header("Crowd")]
        [Tooltip("How many guests exist as actual people at once.")]
        public int maxLiveGuests = 40;
        [Tooltip("Guests stop starting new runs this long before closing.")]
        public float closingHours = 1f;

        public int LiveGuests { get { return _guests.Count; } }

        /// <summary>Average of everyone currently on the mountain. Feeds the rating.</summary>
        public float Happiness { get; private set; }

        public bool LiftsClosing
        {
            get { return clock != null && clock.Hour > clock.closesAt - closingHours; }
        }

        readonly List<Guest> _guests = new List<Guest>();
        Transform _crowd;

        Material[] _jackets;
        Material _trousers, _skin, _gear;
        LodgeFacility _lodgeFacility;
        ParkFacility _parkFacility;

        static GuestDirector _instance;

        public static GuestDirector Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<GuestDirector>();
                return _instance;
            }
        }

        void OnEnable() { _instance = this; }

        void Start()
        {
            if (traffic == null) traffic = FindAnyObjectByType<ResortTraffic>();
            if (ledger == null) ledger = Ledger.Instance;
            if (clock == null) clock = ResortClock.Instance;
            if (mountain == null) mountain = MountainGenerator.Instance;
            if (lodge == null) lodge = LodgeBuilder.Instance;

            _lodgeFacility = FindAnyObjectByType<LodgeFacility>();
            _parkFacility = FindAnyObjectByType<ParkFacility>();

            var crowd = new GameObject("Guests");
            crowd.hideFlags = HideFlags.DontSaveInEditor;
            _crowd = crowd.transform;

            _jackets = new Material[7];
            for (int i = 0; i < _jackets.Length; i++) _jackets[i] = GuestAppearance.Jacket(i);

            _trousers = MaterialFactory.Create("GuestTrousers", new Color(0.14f, 0.16f, 0.24f), 0.15f);
            _skin = MaterialFactory.Create("GuestHelmet", new Color(0.16f, 0.17f, 0.20f), 0.32f);
            _gear = MaterialFactory.Create("GuestGear", new Color(0.30f, 0.62f, 0.82f), 0.35f);

            if (traffic != null)
            {
                // Guests pay for themselves from here on.
                traffic.bookIncomeDirectly = false;
                traffic.GuestArrived += OnArrival;
            }
        }

        void OnDestroy()
        {
            if (traffic != null) traffic.GuestArrived -= OnArrival;
            if (_crowd != null) Destroy(_crowd.gameObject);
        }

        void Update()
        {
            float total = 0f;
            for (int i = _guests.Count - 1; i >= 0; i--)
            {
                if (_guests[i] == null) { _guests.RemoveAt(i); continue; }
                total += _guests[i].happiness;
            }

            Happiness = _guests.Count > 0 ? total / _guests.Count : 0.7f;
        }

        // ---------------- arrivals ------------------------------------------

        void OnArrival(float expectedSpend)
        {
            if (ledger != null) ledger.Earn(LedgerLine.Tickets, traffic.ticketPrice);

            // Everyone pays. Only some of them get a body.
            if (_guests.Count >= maxLiveGuests || mountain == null)
            {
                BookVirtualSpending();
                BookBuildingTrade(null);
                return;
            }

            Spawn();
        }

        /// <summary>
        /// Every building the player put down takes its own cut of a guest.
        /// The building decides whether it gets one and how much, so adding a
        /// building never means changing this.
        /// </summary>
        void BookBuildingTrade(Guest guest)
        {
            if (ledger == null) return;

            for (int i = 0; i < PlacedBuilding.All.Count; i++)
            {
                PlacedBuilding building = PlacedBuilding.All[i];
                if (building == null) continue;

                float amount = building.Trade();
                if (amount <= 0f) continue;

                ledger.Earn(building.Line, amount);
                if (guest != null) guest.money -= amount;
            }
        }

        /// <summary>Amenities put people in a better mood just by being there.</summary>
        public float AmenityHappiness()
        {
            float total = 0f;
            for (int i = 0; i < PlacedBuilding.All.Count; i++)
            {
                PlacedBuilding building = PlacedBuilding.All[i];
                if (building != null) total += building.HappinessBonus;
            }

            return Mathf.Min(total, 0.22f);
        }

        void BookVirtualSpending()
        {
            if (ledger == null || traffic == null) return;

            if (Random.value < traffic.lodgeVisitChance)
                ledger.Earn(LedgerLine.Lodge, traffic.lodgeSpend * LodgeMultiplier());

            if (Random.value < traffic.parkVisitChance)
                ledger.Earn(LedgerLine.TerrainPark, traffic.parkSpend * ParkMultiplier());

            if (Random.value < traffic.rentalChance)
                ledger.Earn(LedgerLine.Rentals, traffic.rentalSpend);
        }

        void Spawn()
        {
            var go = new GameObject("Guest");
            go.transform.SetParent(_crowd, false);
            go.hideFlags = HideFlags.DontSaveInEditor;

            Transform skis, board;
            GuestAppearance.Build(go.transform,
                                  _jackets[Random.Range(0, _jackets.Length)],
                                  _trousers, _skin, _gear, out skis, out board);

            var guest = go.AddComponent<Guest>();
            guest.ability = Mathf.Clamp01(Random.value * 0.85f + 0.1f);
            guest.money = Random.Range(90f, 320f);
            guest.happiness = Mathf.Clamp01(Random.Range(0.60f, 0.85f) + AmenityHappiness());
            guest.gear = Random.value < 0.32f ? LocomotionKind.Snowboard : LocomotionKind.Ski;
            guest.preferredPiste = ChoosePiste(guest.ability);
            guest.run = mountain.TrailAt(guest.preferredPiste);

            guest.Begin(this, ArrivalPoint(), skis, board);

            _guests.Add(guest);
            BookBuildingTrade(guest);
        }

        /// <summary>
        /// Better skiers pick the harder run; beginners stay on the green.
        /// Closed runs are not offered. Returns -1 when the resort has nothing
        /// open, which is the state a brand new resort is in.
        /// </summary>
        int ChoosePiste(float ability)
        {
            if (mountain == null || mountain.TrailCount == 0) return -1;

            int best = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < mountain.TrailCount; i++)
            {
                Trail run = mountain.TrailAt(i);
                if (run == null || !run.open) continue;

                float demanded;
                switch (run.grade)
                {
                    case TrailGrade.Green: demanded = 0.15f; break;
                    case TrailGrade.Blue: demanded = 0.42f; break;
                    case TrailGrade.Black: demanded = 0.72f; break;
                    default: demanded = 0.9f; break;
                }

                // Nobody takes a run well beyond them; the rest is preference.
                if (demanded > ability + 0.25f) continue;

                float score = Mathf.Abs(ability - demanded) + Random.value * 0.18f;
                if (score < bestScore) { bestScore = score; best = i; }
            }

            return best;
        }

        /// <summary>The car park: just off the base area, beside the lodge.</summary>
        Vector3 ArrivalPoint()
        {
            Vector3 point = lodge != null
                ? lodge.EntrancePosition + new Vector3(-26f, 0f, -14f)
                : new Vector3(-40f, 0f, 12f);

            point.x += Random.Range(-6f, 6f);
            point.z += Random.Range(-4f, 4f);
            point.y = mountain != null ? mountain.SampleHeight(point.x, point.z) : point.y;

            return point;
        }

        // ---------------- money ---------------------------------------------

        public void SpendAtLodge(Guest guest)
        {
            if (ledger == null || traffic == null || guest == null) return;

            if (Random.value < traffic.rentalChance)
            {
                ledger.Earn(LedgerLine.Rentals, traffic.rentalSpend);
                guest.money -= traffic.rentalSpend;
            }

            if (Random.value >= traffic.lodgeVisitChance) return;

            float amount = traffic.lodgeSpend * LodgeMultiplier();
            ledger.Earn(LedgerLine.Lodge, amount);
            guest.money -= amount;
        }

        public void SpendOnTheHill(Guest guest)
        {
            if (ledger == null || traffic == null || guest == null) return;
            if (Random.value >= traffic.parkVisitChance) return;

            float amount = traffic.parkSpend * ParkMultiplier();
            ledger.Earn(LedgerLine.TerrainPark, amount);
            guest.money -= amount;
        }

        float LodgeMultiplier()
        {
            return _lodgeFacility != null ? _lodgeFacility.SpendMultiplier : 1f;
        }

        float ParkMultiplier()
        {
            return _parkFacility != null ? _parkFacility.SpendMultiplier : 1f;
        }

        // ---------------- leaving --------------------------------------------

        public void Release(Guest guest)
        {
            if (guest == null) return;

            _guests.Remove(guest);
            Destroy(guest.gameObject);
        }

        /// <summary>How many live guests are on a given run right now.</summary>
        public int GuestsOn(int trailIndex)
        {
            int count = 0;
            for (int i = 0; i < _guests.Count; i++)
            {
                Guest guest = _guests[i];
                if (guest == null) continue;
                if (guest.preferredPiste != trailIndex) continue;
                if (guest.activity != Guest.Activity.Descending) continue;
                count++;
            }
            return count;
        }
    }
}
