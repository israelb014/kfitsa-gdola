namespace Kfitsa
{
    /// All feel numbers in one place. Units: tiles and seconds (1 tile = 18 px).
    public static class Tune
    {
        public const float SimHz = 120f;

        // Ground movement
        public const float WalkSpeed = 5.9f, RunSpeed = 9.6f;
        public const float WalkAccel = 9f, RunAccel = 13f;
        public const float Friction = 12f, SkidDecel = 26f;
        public const float AirAccel = 10f, AirReverse = 16f;

        // Jump: the launch speed and rise gravity blend from walk to run with ground speed
        public const float JumpSpeed = 16.2f, RunJumpSpeed = 19.9f;
        public const float BigJumpScale = 1.1f;    // the big form jumps about a tile higher: a reason to keep it
        public const float RiseGravity = 28f, RunRiseGravity = 34f;
        public const float CutGravity = 98f;       // jump button released while rising
        public const float FallGravity = 72f;
        public const float ApexSpeed = 1.6f, ApexGravityScale = 0.5f; // hang while held near the top
        public const float MaxFall = 17f;
        public const float FastFallScale = 1.35f, FastFallMax = 23f;  // holding down
        public const float Coyote = 0.12f, JumpBuffer = 0.11f;
        public const float CornerNudge = 0.42f;    // head clips a corner by less than this: slide past it
        public const float LedgeAssist = 0.42f;    // feet this close under a ledge's top: step up onto it
        public const float LedgeMaxRise = 5f;      // only near the top of a jump or falling; a fast rise clears it alone
        public const float PlatformCatch = 0.22f;  // feet this far under a moving platform's top: land on it

        // Enemies and hits
        public const float StompBounce = 13f, StompBounceHeld = 18f;
        public const float EnemySpeed = 1.9f, EnemyGravity = 60f;
        public const float Invulnerable = 1.6f;

        // Camera
        public const float CamLookAhead = 3f, CamLookSpeed = 2.2f, CamFollow = 7f;
    }
}
