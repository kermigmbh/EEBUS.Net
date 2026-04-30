using EEBUS.Enums;

namespace TestProject1.Ship
{
    /// <summary>
    /// Prüft, dass alle SHIP-Timeout-Konstanten innerhalb der Spec-Grenzen liegen
    /// und korrekt zueinander geordnet sind.
    ///
    /// EEBUS SHIP TS v1.1.0 – relevante Limits:
    ///   CMI_TIMEOUT                ≤ 30 s
    ///   T_HELLO_INIT               ≤ 240 s
    ///   T_HELLO_PROLONG_THR_INC    ≤ 30 s
    ///   T_HELLO_PROLONG_WAITING_GAP ≤ 15 s
    ///   T_HELLO_PROLONG_MIN        ≤ 1 s
    ///
    /// Alle Tests sind heute grün (Regressionswächter).
    /// </summary>
    public class ShipTimeoutConstantsTests
    {
        // ── CMI_TIMEOUT ──────────────────────────────────────────────────────────

        [Fact]
        public void CmiTimeout_IsWithinSpecLimit()
        {
            // Spec: max. 30 000 ms
            Assert.True(
                SHIPMessageTimeout.CMI_TIMEOUT <= 30_000,
                $"CMI_TIMEOUT ({SHIPMessageTimeout.CMI_TIMEOUT} ms) überschreitet das Spec-Limit von 30 000 ms.");
        }

        [Fact]
        public void CmiTimeout_IsStrictlyPositive()
        {
            Assert.True(
                SHIPMessageTimeout.CMI_TIMEOUT > 0,
                "CMI_TIMEOUT muss größer als 0 sein.");
        }

        // ── T_HELLO_INIT ─────────────────────────────────────────────────────────

        [Fact]
        public void HelloInit_IsWithinSpecLimit()
        {
            // Spec: max. 240 000 ms
            Assert.True(
                SHIPMessageTimeout.T_HELLO_INIT <= 240_000,
                $"T_HELLO_INIT ({SHIPMessageTimeout.T_HELLO_INIT} ms) überschreitet das Spec-Limit von 240 000 ms.");
        }

        // ── Prolongation-Konstanten ───────────────────────────────────────────────

        [Fact]
        public void ProlongationThreshold_IsWithinSpecLimit()
        {
            // Spec: max. 30 000 ms
            Assert.True(
                SHIPMessageTimeout.T_HELLO_PROLONG_THR_INC <= 30_000,
                $"T_HELLO_PROLONG_THR_INC ({SHIPMessageTimeout.T_HELLO_PROLONG_THR_INC} ms) " +
                "überschreitet das Spec-Limit von 30 000 ms.");
        }

        [Fact]
        public void ProlongationWaitingGap_IsWithinSpecLimit()
        {
            // Spec: max. 15 000 ms
            Assert.True(
                SHIPMessageTimeout.T_HELLO_PROLONG_WAITING_GAP <= 15_000,
                $"T_HELLO_PROLONG_WAITING_GAP ({SHIPMessageTimeout.T_HELLO_PROLONG_WAITING_GAP} ms) " +
                "überschreitet das Spec-Limit von 15 000 ms.");
        }

        [Fact]
        public void ProlongationMin_IsWithinSpecLimit()
        {
            // Spec: max. 1 000 ms
            Assert.True(
                SHIPMessageTimeout.T_HELLO_PROLONG_MIN <= 1_000,
                $"T_HELLO_PROLONG_MIN ({SHIPMessageTimeout.T_HELLO_PROLONG_MIN} ms) " +
                "überschreitet das Spec-Limit von 1 000 ms.");
        }

        // ── Reihenfolge-Invarianten ───────────────────────────────────────────────

        [Fact]
        public void HelloTimeouts_AreOrderedDescending()
        {
            // T_HELLO_INIT  ≥  T_HELLO_PROLONG_THR_INC  ≥  T_HELLO_PROLONG_WAITING_GAP  ≥  T_HELLO_PROLONG_MIN
            Assert.True(
                SHIPMessageTimeout.T_HELLO_INIT >= SHIPMessageTimeout.T_HELLO_PROLONG_THR_INC,
                "T_HELLO_INIT muss ≥ T_HELLO_PROLONG_THR_INC sein.");

            Assert.True(
                SHIPMessageTimeout.T_HELLO_PROLONG_THR_INC >= SHIPMessageTimeout.T_HELLO_PROLONG_WAITING_GAP,
                "T_HELLO_PROLONG_THR_INC muss ≥ T_HELLO_PROLONG_WAITING_GAP sein.");

            Assert.True(
                SHIPMessageTimeout.T_HELLO_PROLONG_WAITING_GAP >= SHIPMessageTimeout.T_HELLO_PROLONG_MIN,
                "T_HELLO_PROLONG_WAITING_GAP muss ≥ T_HELLO_PROLONG_MIN sein.");
        }

        [Fact]
        public void HelloInit_AllowsMultipleProlongationCycles()
        {
            // T_HELLO_INIT muss mindestens 2 Prolongation-Zyklen erlauben,
            // sonst ist die Prolongation-Mechanik sinnlos.
            int maxCycles = SHIPMessageTimeout.T_HELLO_INIT / SHIPMessageTimeout.T_HELLO_PROLONG_THR_INC;
            Assert.True(
                maxCycles >= 2,
                $"T_HELLO_INIT / T_HELLO_PROLONG_THR_INC = {maxCycles}; erwartet ≥ 2.");
        }
    }
}
