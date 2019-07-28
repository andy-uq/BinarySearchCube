using System;
using System.Collections.Generic;
using System.Linq;

namespace Cube
{
	public partial class Cube
	{
		private class WNode
		{
			private XNode[] _axisX;
			private int[]   _volumeX;
			private int[]   _countY;
			private int[]   _floorX;

			public WNode(int length)
			{
				_floorX = new int[length];
				_countY = new int[length];

				_axisX = new XNode[length];
				_volumeX = new int[length];
			}

			public int Floor => _floorX[0];

			public (int x, int y, int z) FindKeyOffset(int key, int countX)
			{
				int x;
				var mid = x = countX - 1;

				while (mid > 3)
				{
					mid /= 2;

					if (key < _floorX[x - mid]) x -= mid;
				}

				while (key < _floorX[x]) 
					x--;

				var (y, z) = _axisX[x].FindKeyOffset(key, _countY[x]);

				return (x, y, z);
			}

			
			public bool FindIndexForwardInW(int index, int w, int volumeW, int countX, ref int total, out Offset? offset)
			{
				if (total + volumeW > index)
				{
					if (index > total + volumeW / 2)
					{
						total += volumeW;
						{
							offset = FindIndexBackwardInX(index, countX, ref total, out var xyz)
								? new Offset(w, xyz.x, xyz.y, xyz.z)
								: (Offset?) null;

							return true;
						}
					}

					if (FindIndexForwardInX(index, countX, ref total, out var xyz1))
					{
						offset = new Offset(w, xyz1.x, xyz1.y, xyz1.z);
						return true;
					}
				}

				offset = default;
				return false;
			}

			public bool FindIndexBackwardInW(int index, int w, int volumeW, int countX, ref int total, out Offset? offset)
			{
				if (total - volumeW <= index)
				{
					if (index < total - volumeW / 2)
					{
						total -= volumeW;
						{
							offset = FindIndexForwardInX(index, countX, ref total, out var xyz1)
								? new Offset(w, xyz1.x, xyz1.y, xyz1.z)
								: (Offset?) null;

							return true;
						}
					}

					if (FindIndexBackwardInX(index, countX, ref total, out var xyz))
					{
						offset = new Offset(w, xyz.x, xyz.y, xyz.z);
						return true;
					}
				}

				offset = null;
				return false;
			}

			private bool FindIndexForwardInX(int index, int xCount, ref int total, out (int x, int y, int z) xyz)
			{
				for (var x = 0; x < xCount; x++)
				{
					var nodeX = _axisX[x];

					if (total + _volumeX[x] > index)
					{
						(int y, int z) yz;

						if (index > total + _volumeX[x] / 2)
						{
							total += _volumeX[x];

							if (nodeX.FindIndexBackwardInY(index, _countY[x], ref total, out yz))
							{
								xyz = (x, yz.y, yz.z);
								return true;
							}

							xyz = default;
							return false;
						}

						if (nodeX.FindIndexForwardInY(index, _countY[x], ref total, out yz))
						{
							xyz = (x, yz.y, yz.z);
							return true;
						}
					}

					total += _volumeX[x];
				}

				xyz = default;
				return false;
			}

			private bool FindIndexBackwardInX(int index, int countX, ref int total, out (int x, int y, int z) xyz)
			{
				for (var x = countX - 1; x >= 0; x--)
				{
					var nodeX = _axisX[x];
					if (total - _volumeX[x] <= index)
					{
						(int y, int z) yz;

						if (index < total - _volumeX[x] / 2)
						{
							total -= _volumeX[x];

							if (nodeX.FindIndexForwardInY(index, _countY[x], ref total, out yz))
							{
								xyz = (x, yz.y, yz.z);
								return true;
							}

							xyz = default;
							return false;
						}

						if (nodeX.FindIndexBackwardInY(index, _countY[x], ref total, out yz))
						{
							xyz = (x, yz.y, yz.z);
							return true;
						}
					}

					total -= _volumeX[x];
				}

				xyz = default;
				return false;
			}

			private XNode InsertX(int x, int countX, int length)
			{
				if (countX % CapacityGrowthRate == 0 && countX < length)
				{
					Resize(length);
				}

				if (countX != x + 1)
				{
					Move(x, x + 1, countX - x - 1);
				}

				_axisX[x] = new XNode(length);
				return _axisX[x];
			}

			private void Resize(int length)
			{
				Array.Resize(ref _floorX, length);
				Array.Resize(ref _axisX, length);
				Array.Resize(ref _volumeX, length);
				Array.Resize(ref _countY, length);
			}

			private void Move(int sourceIndex, int destinationIndex, int length)
			{
				Array.Copy(_floorX, sourceIndex, _floorX, destinationIndex, length);
				Array.Copy(_axisX, sourceIndex, _axisX, destinationIndex, length);
				Array.Copy(_volumeX, sourceIndex, _volumeX, destinationIndex, length);
				Array.Copy(_countY, sourceIndex, _countY, destinationIndex, length);
			}

			public void SplitY(int x, int y, int length)
			{
				var nodeX = _axisX[x];

				_countY[x]++;
				var countY = _countY[x];
				
				nodeX.InsertY(y + 1, length, countY);
				nodeX.SplitY(y);
			}

			public void SplitX(int x, int countX, int length)
			{
				var a = _axisX[x];
				var b = InsertX(x + 1, countX, length);

				_countY[x + 1] = _countY[x] / 2;
				_countY[x] -= _countY[x + 1];

				a.SplitX(b, _countY[x], _countY[x + 1]);
				
				var volume = a.Volume(_countY[x]);
				_volumeX[x + 1] = _volumeX[x] - volume;
				_volumeX[x] = volume;

				_floorX[x + 1] = b.Floor;
			}

			public void SplitW(WNode target, int offset, int length)
			{
				Array.Copy(_floorX, offset, target._floorX, 0, length);
				Array.Copy(_axisX, offset, target._axisX, 0, length);
				Array.Copy(_volumeX, offset, target._volumeX, 0, length);
				Array.Copy(_countY, offset, target._countY, 0, length);
			}

			public void MergeW(WNode source, int offset, int length, int capacity)
			{
				Resize(capacity);

				Array.Copy(source._floorX, 0, _floorX, offset, length);
				Array.Copy(source._axisX, 0, _axisX, offset, length);
				Array.Copy(source._volumeX, 0, _volumeX, offset, length);
				Array.Copy(source._countY, 0, _countY, offset, length);

			}

			public void MergeX(int x1, int x2, int capacity)
			{
				var a = _axisX[x1];
				var b = _axisX[x2];

				var length = _countY[x2];
				var offset = _countY[x1];
				a.MergeX(b, offset, length, capacity);

				_countY[x1] += _countY[x2];
				_volumeX[x1] += _volumeX[x2];
			}

			public void RemoveX(int x, int countX)
			{
				var length = countX - x;

				Array.Copy(_floorX, x + 1, _floorX, x, length);
				Array.Copy(_axisX, x + 1, _axisX, x, length);
				Array.Copy(_volumeX, x + 1, _volumeX, x, length);
				Array.Copy(_countY, x + 1, _countY, x, length);
			}

			public void SetFloor(int key)
			{
				_floorX[0] = key;
				_axisX[0].SetFloor(key);
			}

			public int ResetFloor(int x) => _floorX[x] = _axisX[x].Floor;

			public int RemoveY(int x)
			{
				_countY[x]--;
				return _countY[x];
			}

			public int CountY(int x) => _countY[x];

			public IEnumerable<int> Keys(int x) => Enumerable.Range(0, _countY[x]).SelectMany(y => Keys(x, y));
			public IEnumerable<int> Keys(int x, int y) => _axisX[x].Keys(y);

			public IEnumerable<string> Values(int x) => Enumerable.Range(0, _countY[x]).SelectMany(y => Values(x, y));
			public IEnumerable<string> Values(int x, int y) => _axisX[x].Values(y);

			public IEnumerable<(int key, string value)> KeyValues(int x) => Enumerable.Range(0, _countY[x]).SelectMany(y => KeyValues(x, y));
			public IEnumerable<(int key, string value)> KeyValues(int x, int y) => _axisX[x].KeyValues(y);

			public void ResetW()
			{
				var xNode = _axisX[0] = new XNode(CapacityGrowthRate);
				xNode.ResetY();

				_countY[0] = 1;
				_volumeX[0] = 0;
			}

			public int VolumeX(int x) => _volumeX[x];

			public string RemoveZ(int x, int y, int z)
			{
				_volumeX[x]--;
				return _axisX[x].RemoveZ(y, z);
			}

			public int AddZ(int x, int y, int z, (int key, string value) value)
			{
				_volumeX[x]++;
				return _axisX[x].AddZ(y, z, value);
			}

			public (int key, string value) Get(int x, int y, int z) => _axisX[x].Get(y, z);
			public string Set(int x, int y, int z, string value) => _axisX[x].Set(y, z, value);

			public int Key(int x, int y, int z) => _axisX[x].Key(y, z);

			public int CountZ(int x, int y) => _axisX[x].CountZ(y);

			public void ResetFloor(int x, int y) => _axisX[x].ResetFloor(y);

			public void MergeY(int x, int y1, int y2) => _axisX[x].MergeY(y1, y2);

			public void RemoveNode(int x, int y, int countY) => _axisX[x].RemoveY(y, countY);
		}
	}
}