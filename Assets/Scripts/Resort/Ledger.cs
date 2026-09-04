using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SnowBound.Resort
{
    public enum LedgerLine
    {
        Tickets,
        Lodge,
        TerrainPark,
        Rentals,
        Maintenance,
        Construction
    }

    /// <summary>One day's figures, kept once the day is closed.</summary>
    [System.Serializable]
    public class DayRecord
    {
        public int day;
        public float[] lines = new float[System.Enum.GetValues(typeof(LedgerLine)).Length];

        public DayRecord(int day) { this.day = day; }

        public float this[LedgerLine line] { get { return lines[(int)line]; } }

        public float Profit
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < lines.Length; i++) total += lines[i];
                return total;
            }
        }
    }

    /// <summary>
    /// The resort's money, and the only thing allowed to change it.
    ///
    /// Every pound in or out goes through Earn or Spend against a named line,
    /// which is what makes an end of day summary possible at all: the summary
    /// is not computed separately, it is just the day's own record read back.
    /// </summary>
    public class Ledger : MonoBehaviour
    {
        static Ledger _instance;

        public static Ledger Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<Ledger>();
                return _instance;
            }
        }

        public float startingCash = 25000f;

        public float Cash { get; private set; }
        public DayRecord Today { get; private set; }
        public DayRecord Yesterday { get; private set; }

        readonly List<DayRecord> _history = new List<DayRecord>();
        public IReadOnlyList<DayRecord> History { get { return _history; } }

        /// <summary>Raised on every booking, for the little ticker in the HUD.</summary>
        public event System.Action<LedgerLine, float> Booked;

        void OnEnable() { _instance = this; }

        void Awake()
        {
            Cash = startingCash;
            Today = new DayRecord(1);
        }

        public void Earn(LedgerLine line, float amount)
        {
            if (amount <= 0f) return;

            Cash += amount;
            Today.lines[(int)line] += amount;
            if (Booked != null) Booked(line, amount);
        }

        /// <summary>
        /// Spend, if there is enough. Returns false and books nothing when
        /// there is not, so callers can refuse the purchase rather than
        /// quietly going overdrawn.
        /// </summary>
        public bool Spend(LedgerLine line, float amount, bool allowOverdraft = false)
        {
            if (amount <= 0f) return true;
            if (!allowOverdraft && amount > Cash) return false;

            Cash -= amount;
            Today.lines[(int)line] -= amount;
            if (Booked != null) Booked(line, -amount);
            return true;
        }

        /// <summary>Running costs are allowed to push the resort into the red.</summary>
        public void Charge(LedgerLine line, float amount)
        {
            Spend(line, amount, true);
        }

        public void CloseDay(int day)
        {
            Today.day = day;
            Yesterday = Today;
            _history.Add(Today);
            Today = new DayRecord(day + 1);
        }

        // ---------------- formatting -------------------------------------

        public static string Money(float amount)
        {
            return "$" + Mathf.Abs(Mathf.Round(amount)).ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>Money with an explicit sign, for a column of takings.</summary>
        public static string Signed(float amount)
        {
            float rounded = Mathf.Round(amount);
            if (Mathf.Abs(rounded) < 0.5f) return "$0";
            return (rounded > 0f ? "+" : "-") + Money(rounded);
        }
    }
}
