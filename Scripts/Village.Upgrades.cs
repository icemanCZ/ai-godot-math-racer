using Godot;

namespace MathRacer;

public partial class Village
{
    private void SetLevel(int house, int level)
    {
        levels[house] = Mathf.Clamp(level, 0, Upgrades.Length);
        var details = decorations[house];
        if (details.GetChildCount() == 0)
        {
            for (int step = 0; step < Upgrades.Length; step++)
            {
                var part = new Node3D { Name = $"Upgrade{step + 1}" }; details.AddChild(part);
                switch (step)
                {
                    case 0:
                        foreach (float x in new[] { -2.15f, 2.15f })
                        {
                            Art.Block(part, new(x, 1.65f, 3.45f), new(1.7f, .35f, .5f), "a87b61");
                            for (int f = 0; f < 5; f++)
                            {
                                Art.Ball(part, new(x - .65f + f * .32f, 1.9f, 3.45f), new(.19f, .2f, .2f), "56c980");
                                Art.Ball(part, new(x - .65f + f * .32f, 2.1f, 3.45f), Vector3.One * .15f, house % 2 == 0 ? "ff86b3" : "ffe58a");
                            }
                        }
                        break;
                    case 1:
                        foreach (float x in new[] { -3.12f, -1.18f, 1.18f, 3.12f })
                            Art.Block(part, new(x, 2.5f, 3.23f), new(.35f, 1.5f, .15f), "63bfa5");
                        break;
                    case 2:
                        Art.Ball(part, new(.45f, 1.25f, 3.2f), Vector3.One * .13f, "ffd35d");
                        Art.Block(part, new(0, 2.13f, 3.15f), new(.65f, .35f, .08f), "ffe58a"); break;
                    case 3: Art.Block(part, new(0, .1f, 3.5f), new(1.7f, .12f, .8f), "df92ac"); break;
                    case 4: Art.Block(part, new(0, 3.8f, 3.65f), new(2.8f, .25f, 1.7f), "68cfee"); break;
                    case 5:
                        foreach (float x in new[] { -1.25f, 1.25f })
                        {
                            Art.Cylinder(part, new(x, 1.85f, 4.25f), .1f, .1f, 3.7f, "fff1d2");
                            Obstacles.Trunk(part, new(x, 0, 4.25f), .1f, 3.7f);
                        }
                        break;
                    case 6:
                        Art.Block(part, new(0, 4.15f, 3.5f), new(3.2f, .2f, 1.3f), "fff1d2");
                        Art.Block(part, new(0, 4.9f, 4.1f), new(3.2f, .12f, .12f), "ffd35d");
                        for (int i = 0; i < 7; i++) Art.Block(part, new(-1.5f + i * .5f, 4.55f, 4.1f), new(.09f, .8f, .09f), "fff1d2");
                        break;
                    case 7:
                        Art.Ball(part, new(0, 5.35f, 3.64f), new(.7f, .7f, .1f), "fff1d2");
                        Art.Ball(part, new(0, 5.35f, 3.75f), new(.51f, .51f, .08f), "88c7d8");
                        Art.Block(part, new(0, 5.35f, 3.85f), new(.08f, 1, .06f), "fff1d2"); break;
                    case 8:
                        Art.Cylinder(part, new(0, 7.2f, 0), .05f, .05f, 1.4f, "ffd35d");
                        Art.Block(part, new(.4f, 7.7f, 0), new(.8f, .45f, .08f), "63bfa5");
                        Art.Ball(part, new(0, 7.95f, 0), Vector3.One * .15f, "ffd35d"); break;
                    case 9:
                        for (int i = 0; i < 11; i++)
                        {
                            float x = -3.6f + i * .72f;
                            Art.Ball(part, new(x, 3.9f - .3f * Mathf.Sin(i * Mathf.Pi / 10), 3.75f), Vector3.One * .18f, i % 2 == 0 ? "ffe58a" : "ff86b3");
                        }
                        Art.Sign(part, new(0, 3.15f, 3.8f), "KRÁSNÝ DOMOV", 20); break;
                }
            }
        }
        for (int i = 0; i < details.GetChildCount(); i++)
        {
            var part = (Node3D)details.GetChild(i); part.Visible = i < levels[house];
            foreach (var body in part.FindChildren("*", "StaticBody3D", true, false))
                ((StaticBody3D)body).CollisionLayer = i < levels[house] ? 1u : 0u;
        }
        progress[house].Text = $"Domeček {levels[house]} / {Upgrades.Length}";
        Residents[house].SetRewardAvailable(levels[house] < Upgrades.Length);
    }
}
