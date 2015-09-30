using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace MyExtensions
{
	public static class MathExtensions
	{
		private static int[] rand256 = new int[256];
		private static Random random = new Random();

		public static void Shuffle<T>(this IList<T> list, int seed = -1)
		{
			Random random = new Random();

			if (seed != -1)
				random = new Random(seed);

			int size = list.Count;

			//Now shuffle the array using Knuth's algorithm
			for (int i = 0; i < size; i++)
			{
				T temp = list[i];
				int swap = random.Next(i,size);
				list[i] = list[swap];
				list[swap] = temp;
			}
		}

		//Some data in this class needs to be initialized before
		static MathExtensions()
		{
			ResetRand256Array(0);
		}

		//Reinitialize the random values within the RNG array
		private static void ResetRand256Array(int seed)
		{
			Random random = new Random(seed);

			//Fill the "random" array with the values between 0 and 255
			for (int i = 0; i < 256; i++)
				rand256[i] = i;

			//Now shuffle the array using Knuth's algorithm
			for (int i = 0; i < 256; i++)
			{
				int temp = rand256[i];
				int swap = random.Next(256);
				rand256[i] = rand256[swap];
				rand256[swap] = temp;
			}
		}

 		/// <summary>
 		/// Gives the value of linear interpolation between value1 and value2 based on amount
 		/// </summary>
 		/// <param name="value1"></param>
 		/// <param name="value2"></param>
 		/// <param name="amount">A value between 0 and 1</param>
 		/// <returns>value1 if amount == 0, value2 if amount == 1</returns>
		public static double LinearInterpolation(double value1, double value2, double amount)
		{
			return value1 + (value2 - value1) * amount;
		}

		public static double CosineInterpolation(double value1, double value2, double amount)
		{
			double mu2;

			mu2 = (1 - Math.Cos(amount * Math.PI)) / 2;
			return (value1 * (1 - mu2) + value2 * mu2);
		}

		/// <summary>
		/// Gives a value between value1 and value2 based on an S-curve interpolation
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="value2"></param>
		/// <param name="amount">A value between 0 and 1</param>
		/// <returns>A value between value1 and value2 based on amount</returns>
		public static double SCurveInterpolation(double value1, double value2, double amount)
		{
			return value1 + (value2 - value1) * SCurve(amount);
		}

		/// <summary>
		/// Gives an adjusted t based on an S-curve: 6t^5-15t^4+10t^3
		/// </summary>
		/// <param name="t">A value between 0 and 1</param>
		/// <returns>An adjusted value between 0 and 1</returns>
		public static double SCurve(double t)
		{
			return (t * t * t) * (6 * (t * t) - 15 * t + 10);
		}

		/// <summary>
		/// Performs an (admitedly slow) integer power multiplication
		/// </summary>
		/// <param name="integerBase"></param>
		/// <param name="exponent"></param>
		/// <returns></returns>
		public static int IntPow(int integerBase, int exponent)
		{
			int result = 1;

			for (int i = 0; i < exponent; i++)
				result *= integerBase;

			return result;
		}

		public static long LongPow(long longBase, long exponent)
		{
			long result = 1;

			for (int i = 0; i < exponent; i++)
				result *= longBase;

			return result;
		}

		/// <summary>
		/// Performs an integer exponentiation which is faster for larger exponents.
		/// Warning: this function is recursive!
		/// </summary>
		/// <param name="integerBase"></param>
		/// <param name="exponent"></param>
		/// <returns></returns>
		public static int IntPowFast(int integerBase, int exponent)
		{
			int result = 1;

			while (exponent != 0)
			{
				if ((exponent & 1) != 0)
					result *= integerBase;

				integerBase *= integerBase;
				exponent >>= 1;
			}

			return result;
		}

		/// <summary>
		/// Binomial Coefficient calculation (n choose k). Taken from http://stackoverflow.com/questions/12983731/algorithm-for-calculating-binomial-coefficient
		/// </summary>
		/// <param name="n"></param>
		/// <param name="k"></param>
		/// <returns></returns>
		public static double Combination(double n, double k)
		{
			double sum = 0;
			for (long i = 0; i < k; i++)
			{
				sum += Math.Log10(n - i);
				sum -= Math.Log10(i + 1);
			}
			return Math.Pow(10, sum);
		}

		/// <summary>
		/// A random number "generator" which produces values between 0 and 255, but produces the same
		/// random numbers for the same values of index
		/// </summary>
		/// <param name="index">Which random number you want (can be larger than 256) </param>
		/// <returns>A random number between 0 and 255</returns>
		public static int Rand256(int index)
		{
			return rand256[index & 255];
		}

		/// <summary>
		/// A random number "generator" which produces values between 0 and 255, but produces the
		/// same random bumbers for the same indices.
		/// </summary>
		/// <param name="indices">A list of index values</param>
		/// <returns>A random number between 0 and 255</returns>
		public static int Rand256(params int[] indices)
		{
			int finalRandom = 0;

			foreach(int i in indices)
				finalRandom = Rand256(finalRandom + i);

			return finalRandom;
		}

		/// <summary>
		/// Allows you to seed the random number generator
		/// </summary>
		/// <param name="newSeed"></param>
		public static void SeedRand256(int seed)
		{
			ResetRand256Array(seed);
		}

		/// <summary>
		/// Gives the two closest integers to the given floating point number
		/// </summary>
		/// <param name="point"></param>
		/// <returns>The two closest integers to point</returns>
		public static int[] ClosestIntegers(double point)
		{
			return new int[] { (int)Math.Floor(point), (int)Math.Ceiling(point) };
		}

		public static double GaussianRandom(double mean, double standardDeviation)
		{
			double v1, v2, s;

			do
			{
				v1 = 2.0 * random.NextDouble() - 1;
				v2 = 2.0 * random.NextDouble() - 1;

				s = v1 * v1 + v2 * v2;
			} while (s >= 1.0);

			if (s == 0)
				return mean;
			else
				return mean + standardDeviation * v1 * Math.Sqrt(-2 * Math.Log(s) / s);
		}

		public static double ExponentialRandom(double lambda)
		{
			return Math.Log(1 - random.NextDouble()) / -lambda;
		}

		public static double ExponentialRandom(double lambda, Random myRandom)
		{
			return Math.Log(1 - myRandom.NextDouble()) / -lambda;
		}

		/// <summary>
		/// Get an exponential random number in the range of 0 to 1
		/// </summary>
		/// <param name="lambda"></param>
		/// <returns></returns>
		public static double ExponentialRandomFinite(double lambda)
		{
			return ExponentialRandomFinite(lambda, random);
		}
		
		/// <summary>
		/// Get an exponential random number in the range of 0 to 1 from a given random number variable
		/// </summary>
		/// <param name="lambda"></param>
		/// <param name="myRandom"></param>
		/// <returns></returns>
		public static double ExponentialRandomFinite(double lambda, Random myRandom)
		{
			return -Math.Log(1 - (1 - Math.Exp(-lambda)) * myRandom.NextDouble()) / lambda;
		}

		public static int UniformRandom(int max)
		{
			return random.Next(max);
		}
	}
}
