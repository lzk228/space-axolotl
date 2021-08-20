using System;
using JetBrains.Annotations;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;

namespace Content.Shared.Throwing
{
    public abstract class ThrowEvent : HandledEntityEventArgs
    {
        /// <summary>
        ///     The entity that threw <see cref="Thrown"/>.
        /// </summary>
        public IEntity? User { get; }

        /// <summary>
        ///     The entity thrown by <see cref="User"/> that hit <see cref="Target"/>
        /// </summary>
        public IEntity Thrown { get; }

        /// <summary>
        ///     The entity hit with <see cref="Thrown"/> by <see cref="User"/>
        /// </summary>
        public IEntity Target { get; }

        public ThrowEvent(IEntity? user, IEntity thrown, IEntity target)
        {
            User = user;
            Thrown = thrown;
            Target = target;
        }
    }

    public class ThrowHitByEvent : ThrowEvent
    {
        public ThrowHitByEvent(IEntity? user, IEntity thrown, IEntity target) : base(user, thrown, target)
        {
        }
    }

    public class ThrowDoHitEvent : ThrowEvent
    {
        public ThrowDoHitEvent(IEntity? user, IEntity thrown, IEntity target) : base(user, thrown, target)
        {
        }
    }
}
