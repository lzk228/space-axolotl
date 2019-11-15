using System;
using System.Collections.Generic;
using Content.Server.Interfaces.Chat;
using Content.Shared.Jobs;
using Robust.Shared.IoC;

namespace Content.Server.Mobs.Roles
{
    public class Job : Role
    {
        private readonly JobPrototype _jobPrototype;

        public Job(Mind mind, JobPrototype jobPrototype) : base(mind)
        {
            _jobPrototype = jobPrototype;
            Name = jobPrototype.Name;
        }

        public override string Name { get; }

        public override void Greet()
        {
            base.Greet();

            var chat = IoCManager.Resolve<IChatManager>();
            chat.DispatchServerMessage(
                Mind.Session,
                String.Format("You're new a {0}. Do your best!", Name));
        }
    }


}
