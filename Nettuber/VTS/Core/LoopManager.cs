namespace Nettuber
{

    public class LoopManager
    {
        private readonly CancellationTokenSource _cancelToken;
        private readonly Task _tickLoop = null;
        private readonly int _tickInterval = 100;

        private readonly MeshLocator meshLocator;

        public LoopManager(MeshLocator meshLocator)
        {
            this.meshLocator = meshLocator;
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
        }

        private async Task TickLoop(CancellationToken token)
        {
            float intervalInSeconds = ((float)this._tickInterval) / 1000f;
            while (!token.IsCancellationRequested)
            {
                Tick(intervalInSeconds);
                await Task.Delay(this._tickInterval);
            }
        }
    }
}
