using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace CubeTests
{
	public class SetByIndex
	{
		[Fact]
		public void AddForward()
		{
			var keys = Enumerable.Range(1, 1000).ToArray();
			var cube = AddAndVerifyKeys(keys);

			for (var i = 0; i < 1000; i++)
			{
				var val = $"{keys[i]:d4}";
				cube.SetIndex(i, val);

				var (_, actual) = cube.GetIndex(i);
				actual.ShouldBe(val);
			}
		}

		private static Cube.Cube AddAndVerifyKeys(int[] keys)
		{
			var cube = new Cube.Cube();
			foreach (var key in keys)
			{
				cube.SetKey(key, Convert.ToString(key));
			}

			cube.Validate();
			return cube;
		}

	}

	public class RemoveItems
	{
		[Fact]
		public void RemoveRandom()
		{
			var keys = Enumerable.Range(1, 1000).ToArray();
			var cube = AddAndVerifyKeys(keys);

			var rand = new Random();
			var remove = new HashSet<int>();
			while (remove.Count < 500)
			{
				var target = rand.Next(keys.Length);
				remove.Add(target);
			}

			foreach (var key in remove)
			{
				cube.DeleteKey(key);
			}

			keys = keys.Except(remove).ToArray();

			for (var i = 0; i < 500; i++)
			{
				var (key, _) = cube.GetIndex(i);
				key.ShouldBe(keys[i]);
			}
		}

		private static Cube.Cube AddAndVerifyKeys(int[] keys)
		{
			var cube = new Cube.Cube();
			foreach (var key in keys)
			{
				cube.SetKey(key, Convert.ToString(key));
			}

			cube.Validate();
			return cube;
		}

	}
}