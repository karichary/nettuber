using VTS.Core;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WatsonWebserver.Core;
using System.Security.Cryptography.X509Certificates;
using System.Security.AccessControl;

namespace Nettuber
{
    public class AnimationManager
    {    


        private readonly MeshLocator meshLocator;
        public IVTSLogger logger;
        private readonly CoreVTSPlugin plugin;

        float elapsed;

        private readonly ConcurrentDictionary<string, AnimationBehavior> behaviors = new ConcurrentDictionary<string, AnimationBehavior>();
        public abstract class AnimationBehavior {
            public abstract Task<bool> Tick(float timeDelta);
        }

        public class Circle : AnimationBehavior {

            string centerLocName;
            double speed;
            string itemId;
            float elapsed = 0;
            double radius;
            float frameLength = 200;
            float lastFired = 0;

            CoreVTSPlugin plugin;

            public Circle(CoreVTSPlugin plugin, string itemId, float speed, float radius, string centerLocName)
            {
                this.plugin = plugin;
                this.speed = speed;
                this.radius = radius;
                this.itemId = itemId;
                this.centerLocName = centerLocName;
            }

            public override async Task<bool> Tick(float timeDelta) {
                elapsed += timeDelta;
                if (elapsed - lastFired < frameLength)
                {
                    return false;
                }
                lastFired = elapsed;
                var move = new VTSItemMoveOptions();
                ConsoleVTSLoggerImpl logger = new();
                var pos = PivotPointConversion(new Position(
                    (float)(radius * Math.Cos(speed * elapsed / 1000)),
                    (float)(radius * Math.Sin(speed * elapsed / 1000)),
                    0.33f,
                    (float)(360 * (speed * elapsed / 1000) / (2 * Math.PI) % 360)
                ), new Tuple<float, float>(0, 0)
                );
   
                move.positionX = pos.PosX*9/16; // (float)(radius * Math.Cos(speed * elapsed / 1000));
                move.rotation = pos.Rot; // (float)(360*(speed * elapsed / 1000)/(2*Math.PI) % 360);
                move.timeInSeconds = 0.8f*(frameLength / 1000);
                move.fadeMode = VTSItemMotionCurve.OVERSHOOT;
                move.positionY = pos.PosY; // (float)(radius * Math.Sin(speed * elapsed / 1000));
                move.size = pos.Size;
                move.userCanStop = true;

                var entry = new VTSItemMoveEntry(itemId, move);
                var resp = await plugin.MoveItem([entry]);
                return false;
            }
        }
        public AnimationManager(IVTSLogger logger, CoreVTSPlugin plugin, MeshLocator meshLocator)
        {
            this.logger = logger;
            this.meshLocator = meshLocator;
            this.plugin = plugin;
        }
        public struct Position {
            public Position(float pX, float pY, float size, float rotation)
            {
                PosX = pX;
                PosY = pY;
                Size = size;
                Rot = rotation;
            }
            public float PosX;
            public float PosY;
            public float Size;
            public float Rot;

        }
        public static Position PivotPointConversion(Position pos, Tuple<float, float> offset)
        {
            
            var rads = 2 * Math.PI * pos.Rot / 360;
            return new Position(
                (float)(pos.PosX + pos.Size * (offset.Item1 * Math.Cos(rads) - offset.Item2 * Math.Sin(rads))),
                (float)(pos.PosY + pos.Size * (offset.Item2 * Math.Cos(rads) + offset.Item2 * Math.Sin(rads))),
                pos.Size,
                pos.Rot
            );
        }

        public async void UnleashJellyfish()
        {
            var itemOpts = new VTSItemListOptions();
            itemOpts.includeItemInstancesInScene = true;
            itemOpts.onlyItemsWithFileName = "ANIM_bachi";
            var jellies = await plugin.GetItemList(itemOpts);
            logger.Log($"{jellies.data.itemInstancesInScene.Count()}");
            if (jellies.data.itemInstancesInScene.Count() > 0)
            {
                behaviors[Guid.NewGuid().ToString()] = new Circle(
                    plugin,
                    jellies.data.itemInstancesInScene[0].instanceID,
                    2,
                    0.2f,
                    "eyeL"
                );
            }
            var model = await plugin.GetCurrentModel();

            await plugin.PinItemToRandom(
                jellies.data.itemInstancesInScene[0].instanceID,
                model.data.modelID,
                "eyebrow_R",
                0,
                VTSItemAngleRelativityMode.RelativeToModel,
                0,
                VTSItemSizeRelativityMode.RelativeToCurrentItemSize
                );
        }

        public void Tick(float timeDelta)
        {
            elapsed += timeDelta;
            Parallel.ForEach(behaviors, async (item) =>
            {
                bool done = await item.Value.Tick(timeDelta);
                if (done) { behaviors.Remove(item.Key, out _); }
            });
        }
        
    }
}
