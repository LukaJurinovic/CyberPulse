namespace CyberPulse.Combat
{
    /// <summary>
    /// Implemented by anything that can receive damage — players, enemies, destructibles.
    /// Weapons and projectiles target this interface rather than concrete types.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Apply <paramref name="amount"/> points of damage.</summary>
        void TakeDamage(int amount);

        /// <summary>True when the object has been destroyed / killed.</summary>
        bool IsDead { get; }
    }
}
