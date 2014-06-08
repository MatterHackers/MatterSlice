difference()
{
	union()
	{
		difference()
		{
			cube([30, 50, 10], center=true);
			translate([0,11,-1]) cube([20, 60, 10], center=true);
		}
		translate([0,-10,0]) cylinder(10, 8, 8, center=true);
	}
	translate([0,-10,0]) cylinder(15, 6, 6, center=true);
}
