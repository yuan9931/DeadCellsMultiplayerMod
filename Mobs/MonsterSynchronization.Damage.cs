using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using dc;
using dc.en;
using dc.h2d;
using dc.libs.heaps.slib;
using dc.libs.heaps.slib._AnimManager;
using dc.pr;
using dc.tool;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerMod.Ghost;
using DeadCellsMultiplayerMod.Interface.ModuleInitializing;
using DeadCellsMultiplayerMod.Mobs.Bosses;
using DeadCellsMultiplayerMod.PortableCore;
using DeadCellsMultiplayerMod.Tools;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;
using ModCore.Events;
using ModCore.Events.Interfaces.Game;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerMod.Mobs.MobsSynchronization
{
    public partial class MobsSynchronization
    {
        private static void RunWithSuppressedMobHitSend(Action action)
        {
            if (action == null)
                return;

            suppressMobHitSendDepth++;
            try
            {
                action();
            }
            finally
            {
                suppressMobHitSendDepth--;
            }
        }

        private void Hook_Mob_onDamage(Hook_Mob.orig_onDamage orig, Mob self, AttackData i)
        {
            var preDamageLife = GetMobLifeOrFallback(self, 0);
            // Lethal damage runs onDie inside orig, which removes the mob from tracking before this hook resumes.
            // Cache ids before orig so hit|life still sends when the mob is already untracked/destroyed.
            var preSyncOk = false;
            var cachedMobSyncId = -1;
            if (self != null && i != null && LobbySession.NetRef != null && IsSyncMob(self))
            {
                preSyncOk = TryGetMobSyncId(self, out cachedMobSyncId);
            }

            // Read the attack's construction input before vanilla resolves the hit: resolution
            // rewrites the per-target damage fields, and AttackData instances are recycled.
            var attackIntentDamage = ReadAttackIntentDamage(i);

            orig(self, i);

            try
            {
                if (self == null || i == null)
                    return;

                var net = LobbySession.NetRef;
                if (net == null)
                    return;

                var isClient = IsClient(net);
                var suppressedClientLethal = isClient && WasClientMobDeathSuppressed(self);
                var tookLifeDelta = self.life < preDamageLife || suppressedClientLethal;
                var becameDead = self.life <= 0 || suppressedClientLethal;
                if (isClient)
                {
                    var heroDamageSuppressed = false;
                    lock (Sync)
                    {
                        if (!clientLastReportedMobLife.TryGetValue(self, out var lastLife))
                        {
                            clientLastReportedMobLife[self] = self.life;
                            heroDamageSuppressed = self.life >= self.maxLife && self.life > 0;
                        }
                        else
                        {
                            if (self.life >= lastLife)
                            {
                                var lethalReport = self.life <= 0 && lastLife > 0 && preDamageLife > 0;
                                if (!lethalReport)
                                    heroDamageSuppressed = true;
                            }

                            clientLastReportedMobLife[self] = self.life;
                        }
                    }

                    if (heroDamageSuppressed)
                        return;
                }
            }
            finally
            {
                TryRecoverClientSyncMobLifeAfterLocalDamage(self, preDamageLife);
                TryRecoverSuppressedClientMobDie(self, preDamageLife);
                TryRecoverSuppressedClientBossDie(self, preDamageLife);
            }
        }

    }
}
