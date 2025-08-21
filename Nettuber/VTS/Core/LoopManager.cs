namespace Nettuber
{

    public class LoopManager
    {
        private readonly CancellationTokenSource _cancelToken;
        private readonly Task _tickLoop = null;
        private readonly int _tickInterval = 100;

        private readonly MeshLocator meshLocator;

        private readonly AnimationManager animationManager;

        public LoopManager(MeshLocator meshLocator, AnimationManager animationManager)
        {
            this.meshLocator = meshLocator;
            this.animationManager = animationManager;
            this._cancelToken = new CancellationTokenSource();
            this._tickLoop = TickLoop(this._cancelToken.Token);

        }

        ~LoopManager()
        {
            Dispose();
        }

        public void Dispose()
        {
            this._cancelToken.Cancel();
        }

        private void Tick(float timeDelta)
        {
            meshLocator.Tick();
            animationManager.Tick(timeDelta);
        }

        private async Task TickLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Tick(this._tickInterval);
                await Task.Delay(this._tickInterval);
            }
        }
    }
}
