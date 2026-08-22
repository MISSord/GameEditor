using System;
using System.Collections.Generic;

namespace EGamePlay
{
    public class PowerModel
    {
        public int power = 1; //权重
    }

    public static class RandomHelper
    {
        private static readonly Random _random = new Random();

        private static byte[] _byte8 = new byte[8];

        public static UInt64 RandUInt64()
        {
            _random.NextBytes(_byte8);
            return BitConverter.ToUInt64(_byte8, 0);
        }

        public static Int64 RandInt64()
        {
            _random.NextBytes(_byte8);
            return BitConverter.ToInt64(_byte8, 0);
        }

        /// <summary>
        /// 获取lower与Upper之间的随机数
        /// </summary>
        /// <param name="lower"></param>
        /// <param name="upper"></param>
        /// <returns></returns>
        public static int RandomNumber(int lower, int upper)
        {
            int value = _random.Next(lower, upper);
            return value;
        }

        public static int RandomRate()
        {
            int value = _random.Next(1, 101);
            return value;
        }

        public static T GetRandom<T>(this List<T> powerModel, out int index) where T : PowerModel
        {
            index = 0;
            int total = 0;
            foreach (var item in powerModel)
            {
                total += item.power;
            }
            if (powerModel.Count == 0)
            {
                index = -1;
                return null;
            }
            if (powerModel.Count == 1 || total == 0)
            {
                return powerModel[0];
            }

            int random = UnityEngine.Random.Range(0, total);
            int rangeMax = 0;

            int length = powerModel.Count;
            for (int i = 0; i < length; i++)
            {
                rangeMax += powerModel[i].power;
                //当随机数小于 rangeMax 说明在范围内
                if (rangeMax > random && powerModel[i].power > 0)
                {
                    index = i;
                    return powerModel[i];
                }
            }
            return null;
        }
    }
}