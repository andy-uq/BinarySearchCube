using System;
using System.Collections;
using System.Collections.Generic;

namespace Cube
{
	public partial class Cube
	{
		public class XNode
		{
			private int[] _countZ;
			private int[] _floor;
			private YNode[] _axisY;

			public XNode(in int size)
			{
				_floor = new int[size];
				_axisY = new YNode[size];
				_countZ = new int[size];
			}

			public int Floor => _floor[0];

			public (int y, int z) FindKeyOffset(int key, int countY)
			{
				int y;
				var mid = y = countY - 1;

				while (mid > 7)
				{
					mid /= 4;

					if (key < _floor[y - mid])
					{
						y -= mid;
						if (key < _floor[y - mid])
						{
							y -= mid;
							if (key < _floor[y - mid])
							{
								y -= mid;
							}
						}
					}
				}

				while (key < _floor[y]) 
					y--;

				var z = _axisY[y].FindZ(key, _countZ[y]);
			
				return (y, z);
			}


			public bool FindIndexForward(int index, int yCount, ref int total, out (int y, int z) offset)
			{
				for (int y = 0; y < yCount; y++)
				{
					if (total + _countZ[y] > index)
					{
						var z = index - total;
						offset = (y, z);
						return true;
					}

					total += _countZ[y];
				}

				offset = default;
				return false;

			}

			public bool FindIndexBackward(int index, int yCount, ref int total, out (int y, int z) offset)
			{
				for (var y = yCount - 1; y >= 0; y--)
				{
					if (total - _countZ[y] <= index)
					{
						var z = _countZ[y] - (total - index);
						offset = (y, z);
						return true;
					}

					total -= _countZ[y];
				}

				offset = default;
				return false;
			}

			public void InsertNode(int y, int mSize, int countY)
			{
				if (countY % BSC_M == 0 && countY < mSize)
				{
					Resize(mSize);
				}

				var destinationIndex = y + 1;
				if (countY != destinationIndex)
				{
					var length = countY - y - 1;
					Array.Copy(_floor, y, _floor, destinationIndex, length);
					Array.Copy(_axisY, y, _axisY, destinationIndex, length);
					Array.Copy(_countZ, y, _countZ, destinationIndex, length);
				}

				_axisY[y] = new YNode();
			}

			private void Resize(int length)
			{
				Array.Resize(ref _floor, length);
				Array.Resize(ref _axisY, length);
				Array.Resize(ref _countZ, length);
			}

			public void SplitY(int y)
			{
				var a = _axisY[y];
				var b = _axisY[y + 1];

				_countZ[y + 1] = _countZ[y] / 2;
				_countZ[y] -= _countZ[y + 1];

				a.Split(b, _countZ[y], _countZ[y + 1]);

				_floor[y + 1] = b.Key(0);
			}

			public void Split(XNode target, int offset, in int length)
			{
				Array.Copy(_floor, offset, target._floor, 0, length);
				Array.Copy(_axisY, offset, target._axisY, 0, length);
				Array.Copy(_countZ, offset, target._countZ, 0, length);
			}

			public void MergeY(int y1, int y2)
			{
				var a = _axisY[y1];
				var b = _axisY[y2];

				a.Merge(b, _countZ[y1], _countZ[y2]);

				_countZ[y1] += _countZ[y2];
			}

			public void Merge(XNode source, in int offset, in int length, in int mSize)
			{
				Resize(mSize);

				Array.Copy(source._floor, 0, _floor, offset, length);
				Array.Copy(source._axisY, 0, _axisY, offset, length);
				Array.Copy(source._countZ, 0, _countZ, offset, length);
			}

			public void RemoveNode(in int y, int countY)
			{
				var length = countY - y;

				Array.Copy(_floor, y + 1, _floor, y, length);
				Array.Copy(_axisY, y + 1, _axisY, y, length);
				Array.Copy(_countZ, y + 1, _countZ, y, length);
			}

			public string RemoveZ(int y, int z)
			{
				var value = Value(y, z);

				_countZ[y]--;
				var countZ = _countZ[y];

				if (countZ != z)
				{
					_axisY[y].Remove(z, countZ);
				}

				return value;
			}

			public int CountZ(int y) => _countZ[y];

			public Span<int> Keys(int y) => _axisY[y].Keys(_countZ[y]);
			public Span<string> Values(int y) => _axisY[y].Values(_countZ[y]);

			public IEnumerable<(int key, string value)> KeyValues(int y) => _axisY[y].KeyValues(_countZ[y]);

			public void Reset(int y)
			{
				_axisY[y] = new YNode();
				_countZ[y] = 0;
			}

			public void SetFloor(int key) => _floor[0] = key;
			public void ResetFloor(int y) => _floor[y] = _axisY[y].Key(0);

			public int Key(int y, int z) => _axisY[y].Key(z);
			public string Value(int y, int z) => _axisY[y].Value(z);

			public (int, string) Get(int y, int z) => _axisY[y].Get(z);
			public string Set(int y, int z, string value) => _axisY[y].Set(z, value);
			public void Set(int y, int z, (int, string) value) => _axisY[y].Set(z, value);

			public int AddZ(int y, int z, (int key, string value) value)
			{
				_countZ[y]++;
				var countZ = _countZ[y];

				if (z + 1 != countZ)
				{
					_axisY[y].Insert(z, countZ);
				}

				_axisY[y].Set(z, value);

				return countZ;
			}

			public void RemoveY(int y, int z, int countZ)
			{
				_axisY[y].Remove(z, countZ);
			}
		}
	}
}