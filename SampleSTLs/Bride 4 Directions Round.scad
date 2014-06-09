diameter = 10;
width = 40;
squar2 = 1.41421356237;
cutoutWidth = width / squar2 / 2;
difference()
{
	cube([width, width, 10], center=true);
	translate([0,-width/2,-2]) rotate([0, 0, 45]) cube([cutoutWidth, cutoutWidth, 10], center=true);
	translate([0,width/2,-2]) rotate([0, 0, 45]) cube([cutoutWidth, cutoutWidth, 10], center=true);
	translate([width/2,0,-2]) rotate([0, 0, 45]) cube([cutoutWidth, cutoutWidth, 10], center=true);
	translate([-width/2,0,-2]) rotate([0, 0, 45]) cube([cutoutWidth, cutoutWidth, 10], center=true);
}
