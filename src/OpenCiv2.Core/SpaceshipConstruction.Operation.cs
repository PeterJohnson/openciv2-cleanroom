using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public sealed partial class SpaceshipConstruction
{
    /// <summary>V07 retained assembly shares the synchronous sequence, including live retry gates
    /// and the trusted raw reply domain. Disposing/failing cannot replay accepted effects.</summary>
    public sealed class Operation : IDisposable
    {
        private readonly SpaceshipConstruction parent;
        private readonly IEnumerator<PartRequest> steps;
        private bool disposed, faulted;
        public int? Result { get; private set; }
        public SpaceshipPartChoice? Pending { get; private set; }
        internal Operation(SpaceshipConstruction parent, int owner, int group)
        {
            this.parent = parent;
            steps = parent.AddSteps(owner, group, value => Result = value).GetEnumerator();
        }
        public SpaceshipPartChoice? Resume(int? reply = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("A failed spaceship assembly cannot resume");
            }

            if (Result is not null)
            {
                return null;
            }

            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null)
                {
                    if (reply is null)
                    {
                        return Pending;
                    }

                    steps.Current.Reply(reply.Value);
                }
                Pending = null;
                if (steps.MoveNext())
                {
                    return Pending = steps.Current.Choice;
                }

                steps.Dispose();
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
}
