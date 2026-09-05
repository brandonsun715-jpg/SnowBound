using UnityEngine;
using SnowBound.Weather;

namespace SnowBound.Resort
{
    /// <summary>
    /// How busy the resort is, and what that is worth.
    ///
    /// Demand is a day's worth of guests, scaled by how good the resort is
    /// and what the weather is doing, then spread across opening hours on a
    /// curve so mornings build and afternoons tail off. Arrivals are
    /// accumulated as fractions and cashed in whole, so the rate can be far
    /// below one guest per frame without ever being lost to rounding.
    ///
    /// Each arrival books its own spending. When real guests exist they take
    /// this over: turn off bookIncomeDirectly and listen to GuestArrived, and
    /// the demand model above carries on unchanged.
    /// </summary>
    public class ResortTraffic : MonoBehaviour
    {
        public ResortClock clock;
        public Ledger ledger;
        public WeatherSystem weather;

        [Header("Demand")]
        [Tooltip("Guests on an average day, before rating and weather.")]
        public float guestsPerDay = 240f;
        [Tooltip("Set by the resort rating once that system exists.")]
        [Range(0f, 5f)] public float rating = 3f;
        public float demandAtWorstRating = 0.35f;
        public float demandAtBestRating = 1.8f;
        [Tooltip("Nobody drives up for a whiteout.")]
        public float demandInStorm = 0.45f;
        public float demandWhenClear = 1.15f;

        [Header("What a guest spends")]
        public float ticketPrice = 62f;
        [Range(0f, 1f)] public float lodgeVisitChance = 0.62f;
        public float lodgeSpend = 19f;
        [Range(0f, 1f)] public float parkVisitChance = 0.20f;
        public float parkSpend = 14f;
        [Range(0f, 1f)] public float rentalChance = 0.34f;
        public float rentalSpend = 41f;

        [Header("Handover")]
        [Tooltip("Off once real guests exist: they pay for themselves instead.")]
        public bool bookIncomeDirectly = true;

        public int GuestsToday { get; private set; }
        public float GuestsPerHour { get; private set; }
        public float DemandToday { get; private set; }

        /// <summary>Raised for each arrival, carrying what that guest will spend.</summary>
        public event System.Action<float> GuestArrived;

        Facility[] _facilities;
        LodgeFacility _lodge;
        ParkFacility _park;
        ResortRating _ratingSource;
        SnowBound.Lifts.Chairlift _anyLift;
        float _pendingArrivals;
        float _lastChargedHour;
        int _countedDay;

        public float DailyUpkeep
        {
            get
            {
                if (_facilities == null) return 0f;

                float total = 0f;
                for (int i = 0; i < _facilities.Length; i++)
                {
                    Facility facility = _facilities[i];
                    if (facility == null || !facility.Operating) continue;

                    // A building still being positioned has not been paid for
                    // and does not cost anything to run yet.
                    var placed = facility as SnowBound.Buildings.PlacedBuilding;
                    if (placed != null && placed.ghost) continue;

                    total += facility.DailyUpkeep;
                }

                return total;
            }
        }

        void Start()
        {
            if (clock == null) clock = ResortClock.Instance;
            if (ledger == null) ledger = Ledger.Instance;
            if (weather == null) weather = WeatherSystem.Instance;

            Rescan();
            _ratingSource = ResortRating.Instance;

            if (clock != null) _lastChargedHour = clock.opensAt;
        }

        /// <summary>
        /// Re-read what the resort owns. Called after something is built, so
        /// a new building starts costing money the hour after it opens.
        /// </summary>
        public void Rescan()
        {
            _facilities = FindObjectsByType<Facility>(FindObjectsSortMode.None);
            _lodge = FindAnyObjectByType<LodgeFacility>();
            _park = FindAnyObjectByType<ParkFacility>();
            _anyLift = FindAnyObjectByType<SnowBound.Lifts.Chairlift>();
        }

        void Update()
        {
            if (clock == null || ledger == null) return;

            if (clock.Day != _countedDay)
            {
                _countedDay = clock.Day;
                GuestsToday = 0;
                _pendingArrivals = 0f;
                _lastChargedHour = clock.opensAt;
            }

            if (_ratingSource != null) rating = _ratingSource.Stars;

            if (clock.Closed) { GuestsPerHour = 0f; return; }

            ChargeUpkeep();
            AdmitGuests(Time.deltaTime);
        }

        /// <summary>
        /// Costs are billed once an hour rather than every frame. Charging
        /// continuously is technically the same money, but it reads as a
        /// constant bleed with takings occasionally interrupting it, which is
        /// exactly backwards from how running a business feels.
        /// </summary>
        void ChargeUpkeep()
        {
            if (_facilities == null || _facilities.Length == 0) return;

            int hours = Mathf.FloorToInt(clock.Hour - _lastChargedHour);
            if (hours < 1) return;

            _lastChargedHour += hours;

            float openHours = Mathf.Max(1f, clock.closesAt - clock.opensAt);
            ledger.Charge(LedgerLine.Maintenance, DailyUpkeep / openHours * hours);
        }

        /// <summary>
        /// Nobody comes skiing at a resort with nothing to ski or no way up.
        /// A lodge on a bare mountain is a building, not a business, which is
        /// the whole reason a new resort has to be built before it earns.
        /// </summary>
        float Operable()
        {
            var mountain = SnowBound.Mountain.MountainGenerator.Instance;
            if (mountain == null) return 0f;

            bool open = false;
            for (int i = 0; i < mountain.TrailCount; i++)
            {
                SnowBound.Mountain.Trail run = mountain.TrailAt(i);
                if (run != null && run.open) { open = true; break; }
            }

            if (!open) return 0f;
            if (_anyLift == null) return 0f;

            return 1f;
        }

        void AdmitGuests(float dt)
        {
            float storm = weather != null ? weather.storminess : 0f;

            DemandToday = guestsPerDay
                        * Mathf.Lerp(demandAtWorstRating, demandAtBestRating, Mathf.Clamp01(rating / 5f))
                        * Mathf.Lerp(demandWhenClear, demandInStorm, storm)
                        * Operable();

            // A half sine across opening hours: quiet at the doors, busy at
            // midday, tailing off by the last lift.
            float shape = Mathf.Sin(Mathf.Clamp01(clock.DayProgress) * Mathf.PI);

            // The area under that curve is 2/pi, so dividing by it keeps the
            // day's total equal to the demand rather than 64% of it.
            float dayLength = Mathf.Max(10f, clock.dayLengthMinutes * 60f);
            float perSecond = DemandToday * shape / ((2f / Mathf.PI) * dayLength);

            GuestsPerHour = perSecond * dayLength / (clock.closesAt - clock.opensAt);

            _pendingArrivals += perSecond * dt;

            int guard = 0;
            while (_pendingArrivals >= 1f && guard++ < 64)
            {
                _pendingArrivals -= 1f;
                Admit();
            }
        }

        void Admit()
        {
            GuestsToday++;

            float spend = ticketPrice;

            if (bookIncomeDirectly)
            {
                ledger.Earn(LedgerLine.Tickets, ticketPrice);

                if (Random.value < lodgeVisitChance)
                {
                    float amount = lodgeSpend * (_lodge != null ? _lodge.SpendMultiplier : 1f);
                    ledger.Earn(LedgerLine.Lodge, amount);
                    spend += amount;
                }

                if (Random.value < parkVisitChance)
                {
                    float amount = parkSpend * (_park != null ? _park.SpendMultiplier : 1f);
                    ledger.Earn(LedgerLine.TerrainPark, amount);
                    spend += amount;
                }

                if (Random.value < rentalChance)
                {
                    ledger.Earn(LedgerLine.Rentals, rentalSpend);
                    spend += rentalSpend;
                }
            }

            if (GuestArrived != null) GuestArrived(spend);
        }
    }
}
