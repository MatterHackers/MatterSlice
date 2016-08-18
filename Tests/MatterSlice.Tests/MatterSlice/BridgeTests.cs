/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MSClipperLib;
using NUnit.Framework;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice.Tests
{
	using Polygons = List<List<IntPoint>>;

	[TestFixture, Category("MatterSlice")]
	public class BridgeAngleTests
	{
		[Test]
		public void TestConvexBottomLayer()
		{
			// Check that we can cross two islands that are both at 45s
			//   /
			//  /     /
			//       /
			{
				string islandToFillString = "x:5655, y:-706,x:-706, y:5656,x:-5655, y:707,x:706, y:-5655,|";
				string layerSupportingIslandString = "x:706, y:6364,x:-706, y:7778,x:-7777, y:707,x:-6363, y:-706,|x:7777, y:-706,x:6363, y:707,x:-706, y:-6363,x:706, y:-7777,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "two islands 45");
				Assert.AreEqual(bridgeAngle, 315, .01);
			}

			// Check that we can close a u shape the right way
			//  ______
			//  |    |
			//  |    |
			//  |    |
			//  |    |
			{
				string islandToFillString = "x:104500, y:109000,x:95501, y:109000,x:95501, y:91001,x:104500, y:91001,|";
				string layerSupportingIslandString = "x:96001, y:108500,x:104000, y:108500,x:104000, y:89501,x:106000, y:89501,x:106000, y:110500,x:94001, y:110500,x:94001, y:89501,x:96001, y:89501,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "upsidedown u");
				Assert.AreEqual(bridgeAngle, 0, .01);
			}

			// Check that we can close a u shape the right way that has a part inside it
			//  ______
			//  |    |
			//  | /\ |
			//  | \/ |
			//  |    |
			{
				string islandToFillString = "x:1, y:0,x:9000, y:0,x:9000, y:19000,x:0, y:19000,|";
				string uOutlineString = "x:0, y:0,x:2000, y:0,x:2000, y:18000,x:8000, y:18000,x:8000, y:0,x:10000, y:0,x:10000, y:20000,x:0, y:20000,|";
				string innerFeatureString = "x:5000, y:5000,x:3000, y:7000,x:5000, y:9000,x:7000, y:7000,|";
				string layerSupportingIslandString = uOutlineString + innerFeatureString;
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "upsidedown u");
				//Assert.AreEqual(bridgeAngle, 0, .01);
			}

			// Check that we can close a u shape the right way
			// Same as last but rotated 45 degrees
			{
				string islandToFillString = "x:124112, y:123394,x:110819, y:136688,x:104313, y:130182,x:117607, y:116889,|";
				string layerSupportingIslandString = "x:118596, y:116748,x:105162, y:130182,x:110819, y:135839,x:124253, y:122405,x:125667, y:123819,x:110819, y:138667,x:102334, y:130182,x:117182, y:115334,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "45 u");
				Assert.AreEqual(bridgeAngle, 45, .01);
			}

			// Check that we can close a u shape the right way
			// Same as last but rotated 31 degrees
			{
				string islandToFillString = "x:122939, y:121055,x:113257, y:137169,x:105372, y:132431,x:115053, y:116317,|";
				string layerSupportingIslandString = "x:115979, y:115941,x:106195, y:132226,x:113052, y:136346,x:122837, y:120061,x:124551, y:121091,x:113736, y:139090,x:103450, y:132910,x:114265, y:114911,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "30 u");
				Assert.AreEqual(bridgeAngle, 31, .01);
			}

			// this is a left open u with curved inner turns
			// _____________
			//              |
			//              |
			//              |
			// _____________|
			{
				string islandToFillString = "x:110550, y:92203,x:111151, y:92519,x:111740, y:93040,x:112186, y:93687,x:112465, y:94420,x:112550, y:95126,x:112550, y:104875,x:112467, y:105565,x:112193, y:106296,x:111752, y:106945,x:111166, y:107470,x:110473, y:107839,x:110203, y:107907,x:110146, y:108050,x:90610, y:108050,x:90610, y:91951,x:110550, y:91951,|";
				string layerSupportingIslandString = "x:122250, y:90207,x:123103, y:90256,x:123948, y:90380,x:124779, y:90577,x:125461, y:90792,x:126251, y:91119,x:127009, y:91514,x:127755, y:91992,x:127879, y:92088,x:126227, y:93740,x:125859, y:93506,x:125279, y:93204,x:124674, y:92953,x:124050, y:92757,x:123411, y:92615,x:122763, y:92530,x:122217, y:92506,x:121456, y:92530,x:120808, y:92615,x:120169, y:92757,x:119545, y:92953,x:118940, y:93204,x:118360, y:93506,x:117808, y:93857,x:117120, y:94411,x:116807, y:94698,x:116365, y:95180,x:115966, y:95699,x:115615, y:96251,x:115313, y:96831,x:115062, y:97436,x:114866, y:98060,x:114724, y:98699,x:114639, y:99347,x:114610, y:100000,x:114639, y:100654,x:114724, y:101302,x:114866, y:101941,x:115062, y:102565,x:115313, y:103170,x:115615, y:103750,x:115966, y:104302,x:116365, y:104821,x:116807, y:105303,x:117374, y:105811,x:117899, y:106202,x:118455, y:106545,x:119040, y:106838,x:119648, y:107080,x:120274, y:107267,x:120915, y:107400,x:121563, y:107476,x:122217, y:107495,x:122870, y:107457,x:123516, y:107363,x:124153, y:107212,x:124774, y:107007,x:125374, y:106747,x:125920, y:106456,x:126227, y:106261,x:127879, y:107913,x:127730, y:108028,x:127009, y:108487,x:126251, y:108882,x:125461, y:109209,x:124645, y:109466,x:123811, y:109651,x:122963, y:109763,x:122109, y:109800,x:89110, y:109800,x:89110, y:106800,x:109300, y:106800,x:109301, y:106785,x:109543, y:106784,x:110011, y:106669,x:110438, y:106444,x:110798, y:106124,x:111072, y:105727,x:111242, y:105277,x:111300, y:104800,x:111300, y:95201,x:111242, y:94722,x:111071, y:94272,x:110797, y:93875,x:110436, y:93555,x:110009, y:93331,x:109541, y:93216,x:109300, y:93216,x:109300, y:93201,x:89110, y:93201,x:89110, y:90201,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "rounded left open u");
				Assert.AreEqual(bridgeAngle, 270, .1);
			}

			// This is a layer from a 20x20 mm box. It should return a -1 (no bridge needed)
			//  _________
			// |         |
			// |         |
			// |         |
			// |_________|
			{
				string islandToFillString = "x:108550, y:91551,x:108450, y:108550,x:91450, y:108450,x:91551, y:91451,|";
				string layerSupportingIslandString = "x:110058, y:90059,x:109942, y:110058,x:89942, y:109942,x:90059, y:89943,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "20x20x10 box");
				Assert.AreEqual(bridgeAngle, -1);
			}

			// This is a layer from a 20x20 mm box with a single cut out. It is shaped like a C. It should return a -1 (no bridge needed)
			//  _________
			// |     ____|
			// |    |
			// |    |____
			// |_________|
			{
				string islandToFillString = "x:108500, y:93501,x:98500, y:93501,x:98500, y:106500,x:108500, y:106500,x:108500, y:108500,x:91501, y:108500,x:91501, y:91501,x:108500, y:91501,|";
				string layerSupportingIslandString = "x:110000, y:95001,x:100000, y:95001,x:100000, y:105000,x:110000, y:105000,x:110000, y:110000,x:90001, y:110000,x:90001, y:90001,x:110000, y:90001,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "20x20x10 C box");
				Assert.AreEqual(bridgeAngle, -1);
			}

			// this is a right open u with curved inner turns
			// _____________
			// |
			// |
			// |
			// |____________
			{
				string islandToFillString = "x:100175, y:108050,x:80235, y:108050,x:80235, y:107798,x:79634, y:107482,x:79045, y:106961,x:78599, y:106314,x:78320, y:105581,x:78235, y:104875,x:78235, y:95126,x:78318, y:94436,x:78592, y:93705,x:79033, y:93056,x:79619, y:92531,x:80312, y:92162,x:80582, y:92094,x:80639, y:91951,x:100175, y:91943,|";
				string layerSupportingIslandString = "x:105665, y:93191,x:81485, y:93201,x:81484, y:93216,x:81242, y:93217,x:80774, y:93332,x:80347, y:93557,x:79987, y:93877,x:79713, y:94274,x:79543, y:94724,x:79485, y:95201,x:79485, y:104800,x:79543, y:105279,x:79714, y:105729,x:79988, y:106126,x:80349, y:106446,x:80776, y:106670,x:81244, y:106785,x:81485, y:106785,x:81485, y:106800,x:101675, y:106800,x:101675, y:109800,x:68535, y:109794,x:67682, y:109745,x:66837, y:109621,x:66006, y:109424,x:65324, y:109209,x:64534, y:108882,x:63776, y:108487,x:63030, y:108009,x:62906, y:107913,x:64558, y:106261,x:64926, y:106495,x:65506, y:106797,x:66111, y:107048,x:66735, y:107244,x:67374, y:107386,x:68022, y:107471,x:68675, y:107500,x:69329, y:107471,x:69977, y:107386,x:70616, y:107244,x:71240, y:107048,x:71845, y:106797,x:72425, y:106495,x:72977, y:106144,x:73496, y:105745,x:73978, y:105303,x:74420, y:104821,x:74819, y:104302,x:75170, y:103750,x:75472, y:103170,x:75723, y:102565,x:75919, y:101941,x:76061, y:101302,x:76146, y:100654,x:76175, y:100000,x:76146, y:99347,x:76061, y:98699,x:75919, y:98060,x:75723, y:97436,x:75472, y:96831,x:75170, y:96251,x:74819, y:95699,x:74420, y:95180,x:73978, y:94698,x:73411, y:94190,x:72886, y:93799,x:72330, y:93456,x:71745, y:93163,x:71137, y:92921,x:70511, y:92734,x:69870, y:92601,x:69221, y:92525,x:68675, y:92501,x:67915, y:92544,x:67269, y:92638,x:66632, y:92789,x:66011, y:92994,x:65411, y:93254,x:64865, y:93545,x:64558, y:93740,x:62906, y:92088,x:63055, y:91973,x:63776, y:91514,x:64534, y:91119,x:65324, y:90792,x:66140, y:90535,x:66974, y:90350,x:67822, y:90238,x:68675, y:90201,x:98665, y:90201,x:98665, y:80291,x:105665, y:80291,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "rounded left open u");
				Assert.AreEqual(bridgeAngle, 90, .1);
			}

			// this is a layer from the medel 90 fan duct that has problems
			{
				string islandToFillString = "x:-6520, y:50281,x:-6076, y:50512,x:-5645, y:50786,x:-5240, y:51097,x:-5215, y:51120,x:-5617, y:51937,x:-5801, y:51770,x:-6153, y:51499,x:-6509, y:51273,x:-7133, y:50948,x:-21314, y:28517,x:-22736, y:24630,|x:25544, y:26620,x:34368, y:50850,x:33437, y:50850,x:23919, y:24713,|";
				string layerSupportingIslandString = "x:1522, y:-17395,x:3033, y:-17196,x:4520, y:-16866,x:5973, y:-16408,x:7381, y:-15825,x:8732, y:-15121,x:10017, y:-14303,x:11226, y:-13375,x:12358, y:-12336,x:13386, y:-11212,x:14313, y:-10003,x:15130, y:-8717,x:15833, y:-7366,x:16415, y:-5957,x:16872, y:-4504,x:17201, y:-3017,x:17399, y:-1506,x:17463, y:13,x:17396, y:1535,x:17196, y:3046,x:16865, y:4533,x:16406, y:5985,x:15822, y:7393,x:15124, y:8732,x:14298, y:10028,x:13369, y:11236,x:12349, y:12349,x:11226, y:13378,x:10017, y:14306,x:8732, y:15124,x:7381, y:15828,x:5899, y:16434,x:4503, y:16873,x:2965, y:17208,x:1522, y:17398,x:0, y:17464,x:-1519, y:17398,x:-3030, y:17199,x:-4517, y:16869,x:-5970, y:16411,x:-7378, y:15828,x:-8729, y:15124,x:-10014, y:14306,x:-11223, y:13378,x:-12355, y:12339,x:-13383, y:11215,x:-14310, y:10006,x:-15127, y:8720,x:-15830, y:7369,x:-16412, y:5960,x:-16869, y:4507,x:-17198, y:3020,x:-17396, y:1509,x:-17460, y:-12,x:-17393, y:-1532,x:-17193, y:-3043,x:-16862, y:-4530,x:-16403, y:-5982,x:-15819, y:-7390,x:-15114, y:-8740,x:-14295, y:-10025,x:-13366, y:-11233,x:-12346, y:-12346,x:-11223, y:-13375,x:-10014, y:-14303,x:-8729, y:-15121,x:-7378, y:-15825,x:-5970, y:-16408,x:-4517, y:-16866,x:-3030, y:-17196,x:-1519, y:-17395,x:0, y:-17461,x:-1432, y:-16399,x:-2856, y:-16208,x:-4258, y:-15900,x:-5628, y:-15468,x:-6955, y:-14916,x:-8228, y:-14253,x:-9440, y:-13484,x:-10580, y:-12609,x:-11637, y:-11637,x:-12609, y:-10580,x:-13484, y:-9440,x:-14253, y:-8228,x:-14916, y:-6955,x:-15468, y:-5628,x:-15900, y:-4258,x:-16208, y:-2856,x:-16399, y:-1432,x:-16461, y:0,x:-16399, y:1435,x:-16208, y:2859,x:-15900, y:4261,x:-15468, y:5631,x:-14916, y:6957,x:-14253, y:8231,x:-13484, y:9443,x:-12609, y:10583,x:-11637, y:11640,x:-10580, y:12612,x:-9440, y:13487,x:-8228, y:14256,x:-6954, y:14919,x:-5628, y:15471,x:-4258, y:15903,x:-2856, y:16211,x:-1432, y:16402,x:0, y:16464,x:1490, y:16395,x:2859, y:16214,x:4261, y:15903,x:5631, y:15471,x:6958, y:14919,x:8231, y:14256,x:9443, y:13487,x:10583, y:12612,x:11640, y:11640,x:12612, y:10583,x:13487, y:9443,x:14256, y:8231,x:14919, y:6957,x:15471, y:5631,x:15903, y:4261,x:16211, y:2859,x:16237, y:2683,x:16402, y:1435,x:16464, y:0,x:16402, y:-1432,x:16211, y:-2856,x:15903, y:-4258,x:15471, y:-5628,x:14919, y:-6955,x:14256, y:-8228,x:13487, y:-9440,x:12612, y:-10580,x:11640, y:-11637,x:10583, y:-12609,x:9443, y:-13484,x:8231, y:-14253,x:6957, y:-14916,x:5631, y:-15468,x:4261, y:-15900,x:2859, y:-16208,x:1435, y:-16399,x:0, y:-16461,|x:2132, y:-24368,x:4248, y:-24089,x:6332, y:-23628,x:8367, y:-22986,x:10339, y:-22169,x:12232, y:-21184,x:14032, y:-20037,x:15725, y:-18738,x:17299, y:-17296,x:18741, y:-15722,x:20040, y:-14029,x:21187, y:-12229,x:22172, y:-10336,x:22989, y:-8364,x:23631, y:-6329,x:24092, y:-4245,x:24371, y:-2129,x:24464, y:0,x:24464, y:23464,x:46850, y:49730,x:46850, y:105580,x:46822, y:106134,x:46751, y:106639,x:46638, y:107136,x:46480, y:107622,x:46282, y:108091,x:46042, y:108542,x:45765, y:108970,x:45451, y:109373,x:45137, y:109717,x:44760, y:110061,x:44355, y:110372,x:43925, y:110646,x:43472, y:110882,x:43001, y:111077,x:42514, y:111231,x:42016, y:111341,x:41510, y:111408,x:41000, y:111430,x:-8997, y:111430,x:-9507, y:111408,x:-10013, y:111341,x:-10511, y:111231,x:-10998, y:111077,x:-11469, y:110882,x:-11922, y:110646,x:-12352, y:110372,x:-12757, y:110061,x:-13164, y:109684,x:-13505, y:109305,x:-13813, y:108898,x:-14083, y:108466,x:-14316, y:108011,x:-14507, y:107539,x:-14658, y:107051,x:-14764, y:106552,x:-14827, y:106046,x:-14847, y:105580,x:-14847, y:49750,x:-24461, y:23484,x:-24461, y:0,x:-24368, y:-2129,x:-24089, y:-4245,x:-23628, y:-6329,x:-22986, y:-8364,x:-22169, y:-10336,x:-21184, y:-12229,x:-20037, y:-14029,x:-18738, y:-15722,x:-17296, y:-17296,x:-15722, y:-18738,x:-14029, y:-20037,x:-12229, y:-21184,x:-10336, y:-22169,x:-8364, y:-22986,x:-6329, y:-23628,x:-4245, y:-24089,x:-2129, y:-24368,x:0, y:-24461,x:-2042, y:-23372,x:-4071, y:-23105,x:-6070, y:-22662,x:-8022, y:-22046,x:-9913, y:-21263,x:-11729, y:-20318,x:-13456, y:-19218,x:-15079, y:-17972,x:-16601, y:-16576,x:-17983, y:-15065,x:-19228, y:-13441,x:-20326, y:-11713,x:-21270, y:-9896,x:-22051, y:-8005,x:-22666, y:-6052,x:-23107, y:-4053,x:-23373, y:-2024,x:-23461, y:209,x:-23461, y:23484,x:-6520, y:50281,x:-6076, y:50512,x:-5645, y:50786,x:-5240, y:51097,x:-4863, y:51440,x:-4519, y:51817,x:-4205, y:52225,x:-3931, y:52655,x:-3695, y:53108,x:-3500, y:53579,x:-3346, y:54066,x:-3236, y:54564,x:-3169, y:55070,x:-3148, y:55594,x:-3175, y:56136,x:-3246, y:56642,x:-3360, y:57139,x:-3518, y:57625,x:-3715, y:58090,x:-3957, y:58546,x:-4235, y:58974,x:-4550, y:59377,x:-4899, y:59752,x:-5280, y:60094,x:-5613, y:60350,x:-9827, y:68535,x:-10778, y:70832,x:-11526, y:73204,x:-12064, y:75631,x:-12389, y:78096,x:-12497, y:80580,x:-12389, y:83064,x:-12064, y:85529,x:-11526, y:87956,x:-10778, y:90328,x:-9827, y:92625,x:-8679, y:94830,x:-7343, y:96927,x:-5829, y:98899,x:-4150, y:100733,x:-2316, y:102412,x:-344, y:103926,x:1750, y:105262,x:3955, y:106410,x:6252, y:107361,x:8624, y:108109,x:11051, y:108647,x:13516, y:108972,x:16000, y:109080,x:18484, y:108972,x:20949, y:108647,x:23376, y:108109,x:25748, y:107361,x:28045, y:106410,x:30250, y:105262,x:32347, y:103926,x:34319, y:102412,x:36153, y:100733,x:37832, y:98899,x:39346, y:96927,x:40682, y:94830,x:41830, y:92625,x:42781, y:90328,x:43529, y:87956,x:44067, y:85529,x:44392, y:83064,x:44500, y:80580,x:44392, y:78096,x:44067, y:75631,x:43529, y:73204,x:42781, y:70832,x:41830, y:68535,x:37616, y:60350,x:37240, y:60061,x:36863, y:59717,x:36519, y:59340,x:36208, y:58935,x:35934, y:58505,x:35698, y:58052,x:35503, y:57581,x:35349, y:57094,x:35239, y:56596,x:35171, y:56057,x:35151, y:55556,x:23464, y:23464,x:23463, y:-17,x:23373, y:-2060,x:23104, y:-4089,x:22659, y:-6087,x:22042, y:-8039,x:21258, y:-9929,x:20311, y:-11744,x:19210, y:-13470,x:17963, y:-15092,x:16592, y:-16589,x:15082, y:-17972,x:13459, y:-19218,x:11732, y:-20318,x:9916, y:-21263,x:8025, y:-22046,x:6073, y:-22662,x:4074, y:-23105,x:2045, y:-23372,x:0, y:-23461,x:-10218, y:103466,x:-11291, y:104745,x:-11291, y:106415,x:-10218, y:107694,x:-8573, y:107984,x:-7127, y:107149,x:-6556, y:105580,x:-7127, y:104011,x:-8573, y:103176,x:39779, y:103466,x:38706, y:104745,x:38706, y:106415,x:39779, y:107694,x:41424, y:107984,x:42870, y:107149,x:43441, y:105580,x:42870, y:104011,x:41424, y:103176,x:-10218, y:53466,x:-11291, y:54745,x:-11291, y:56415,x:-10218, y:57694,x:-8573, y:57984,x:-7127, y:57149,x:-6556, y:55580,x:-7127, y:54011,x:-8573, y:53176,x:39779, y:53466,x:38706, y:54745,x:38706, y:56415,x:39779, y:57694,x:41424, y:57984,x:42870, y:57149,x:43441, y:55580,x:42870, y:54011,x:41424, y:53176,|";
				double bridgeAngle = GetAngleForData(islandToFillString, layerSupportingIslandString, "mendel 90 fan duct");
				//Assert.AreEqual(bridgeAngle, 0, .1);
			}
		}

		private static double GetAngleForData(string islandToFillString, string layerSupportingIslandString, string debugName)
		{
			Polygons islandToFill = PolygonsHelper.CreateFromString(islandToFillString);

			SliceLayer prevLayer = new SliceLayer();
			prevLayer.Islands = new List<LayerIsland>();
			LayerIsland part = new LayerIsland();
			part.IslandOutline = PolygonsHelper.CreateFromString(layerSupportingIslandString);
			prevLayer.Islands.Add(part);
			prevLayer.Islands[0].BoundingBox.Calculate(prevLayer.Islands[0].IslandOutline);

			double bridgeAngle;
			bool foundBridgeAngle = prevLayer.BridgeAngle(islandToFill, out bridgeAngle, debugName);

			return bridgeAngle;
		}
	}
}