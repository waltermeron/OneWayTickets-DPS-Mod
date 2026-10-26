using System;
using Foundations;
using World.Caravans;

namespace OneWayDps {
    public static class VehicleSpeed {
        // ProgressUiView displays accumulated world distance * 2 as meters.
        // CaravanMover_Run integrates position += velocity * (1/120) each second-based tick.
        public const float MetersPerWorldUnit=2f;
        public const string Unit="m/s";
        // Caravan exposes WorldContext.caravan_velocity as its actual velocity.
        // Display horizontal ground speed; jumping does not inflate the reading.
        public static bool TryRead(out float speed) {
            speed=0;
            try {
                CaravanMgr manager;
                var mission=Mission.instance;
                if(mission==null || !mission.try_get_mgr<CaravanMgr>("CaravanMgr",out manager) || manager==null || manager.cell==null || manager.ctx==null)return false;
                return TryToMetersPerSecond(manager.ctx.caravan_velocity.x,out speed);
            } catch(Exception) {return false;}
        }

        public static bool TryNormalizeHorizontal(float horizontalVelocity,out float speed) {
            speed=0;
            if(Single.IsNaN(horizontalVelocity) || Single.IsInfinity(horizontalVelocity))return false;
            speed=Math.Abs(horizontalVelocity);
            return true;
        }
        public static bool TryToMetersPerSecond(float horizontalVelocity,out float speed) {
            speed=0;float magnitude;
            if(!TryNormalizeHorizontal(horizontalVelocity,out magnitude))return false;
            double converted=(double)magnitude*MetersPerWorldUnit;
            if(converted>Single.MaxValue)return false;
            speed=(float)converted;
            return true;
        }
    }
}
