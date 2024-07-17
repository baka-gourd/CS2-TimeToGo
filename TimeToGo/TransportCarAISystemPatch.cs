using System;
using Game.Simulation;

using HarmonyLib;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using Colossal.Logging.Utils;
using Unity.Entities;

namespace TimeToGo
{
    [HarmonyPatch(typeof(TransportCarAISystem))]
    [HarmonyPatch("OnUpdate")]
    public class TransportCarAISystemPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            var newLocal = generator.DeclareLocal(typeof(TransportCarTickJobWithTimer), true);
            newLocal.SetLocalSymInfo("jobHandle");
            // var newLocalIndex = newLocal.LocalIndex;

            foreach (var line in codes)
            {
                if (line.opcode == OpCodes.Ldloca_S && line.operand is LocalBuilder localBuilder && localBuilder.LocalType.GetFriendlyName() == "Game.Simulation.TransportCarAISystem/TransportCarTickJob")
                {
                    line.operand = newLocal;
                    Debug.WriteLine(localBuilder.LocalType.GetFriendlyName());
                    Debug.WriteLine(line);
                }

                if (line.opcode == OpCodes.Newobj && line.operand is ConstructorInfo constructorInfo && constructorInfo.DeclaringType.GetFriendlyName() == "Game.Simulation.TransportCarAISystem/TransportCarTickJob")
                {
                    line.operand = typeof(TransportCarTickJobWithTimer).GetConstructor(Type.EmptyTypes);
                }

                if (line.opcode == OpCodes.Initobj && line.operand is Type operandType && operandType.GetFriendlyName() == "Game.Simulation.TransportCarAISystem/TransportCarTickJob")
                {
                    line.operand = typeof(TransportCarTickJobWithTimer);
                }

                if (line.opcode == OpCodes.Stfld && line.operand is FieldInfo fieldInfo && fieldInfo.DeclaringType.GetFriendlyName() == "Game.Simulation.TransportCarAISystem/TransportCarTickJob")
                {
                    line.operand = typeof(TransportCarTickJobWithTimer).GetField(fieldInfo.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (line.opcode == OpCodes.Call && line.operand is MethodInfo { IsGenericMethod: true } methodInfo && methodInfo.GetGenericMethodDefinition() == typeof(JobChunkExtensions).GetMethod("ScheduleParallel"))
                {
                    var genericMethodDefinition = methodInfo.GetGenericMethodDefinition();

                    var newGenericArguments = methodInfo.GetGenericArguments().Select(t => t.GetFriendlyName() == "Game.Simulation.TransportCarAISystem/TransportCarTickJob" ? typeof(TransportCarTickJobWithTimer) : t).ToArray();
                    var newMethodInfo = genericMethodDefinition.MakeGenericMethod(newGenericArguments);

                    line.operand = newMethodInfo;
                }
            }

            return codes;
        }
    }
}