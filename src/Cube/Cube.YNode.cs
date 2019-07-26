using System;
using System.Collections.Generic;

namespace Cube
{
	public partial class Cube
	{
		public class YNode
		{
			private readonly int[]    _keys;
			private readonly string[] _values;

			public YNode()
			{
				_keys = new int[Cube.BSC_Z_MAX];
				_values = new string[Cube.BSC_Z_MAX];
			}

			public int FindZ(int key, int countZ)
			{
				var z = countZ - 1;
				var mid = z;

				while (mid > 7)
				{
					mid /= 4;

					if (key < _keys[z - mid])
					{
						z -= mid;
						if (key < _keys[z - mid])
						{
							z -= mid;
							if (key < _keys[z - mid])
							{
								z -= mid;
							}
						}
					}
				}

				while (key < _keys[z])
					z--;

				return z;
			}

			public void Split(YNode target, int offset, int length)
			{
				Array.Copy(_keys, offset, target._keys, 0, length);
				Array.Copy(_values, offset, target._values, 0, length);
			}

			public void Merge(YNode source, int offset, in int length)
			{
				Array.Copy(source._keys, 0, _keys, offset, length);
				Array.Copy(source._values, 0, _values, offset, length);
			}

			public void Insert(int z, int countZ)
			{
				var length = countZ - z - 1;
				Array.Copy(_keys, z, _keys, z + 1, length);
				Array.Copy(_values, z, _values, z + 1, length);
			}

			public void Remove(int z, int countZ)
			{
				var length = countZ - z;
				Array.Copy(_keys, z + 1, _keys, z, length);
				Array.Copy(_values, z + 1, _values, z, length);
			}

			public void Set(int z, (int key, string value) value)
			{
				_keys[z] = value.key;
				_values[z] = value.value;
			}

			public string Set(int z, string value)
			{
				var previous = _values[z];
				_values[z] = value;
				return previous;
			}

			public (int key, string value) Get(int z) => (_keys[z], _values[z]);

			public int Key(int z) => _keys[z];
			public string Value(int z) => _values[z];

			public Span<int> Keys(int countZ) => _keys.AsSpan(0, countZ);
			public Span<string> Values(int countZ) => _values.AsSpan(0, countZ);

			public IEnumerable<(int key, string value)> KeyValues(int countZ)
			{
				for (var z = 0; z < countZ; z++)
				{
					yield return (_keys[z], _values[z]);
				}
			}
		}
	}
}