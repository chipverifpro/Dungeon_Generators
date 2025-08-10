<H1>Dungeon Generators in Unity</H1>

Here are a few algorithms I've been trying out to make some nice random dungeons for a Rogue-like game.
</P>
Simplest is the square rooms with orthographic corridors.
<br>
<img width="401" height="380" alt="Rectangles_Orthographic" src="https://github.com/user-attachments/assets/a935c4dd-c8d9-4774-a6c8-3b568f1e76ed" />
<br>
Next, replace those square rooms with ovals that are allowed to overlap.  Connect them with straight corridors.
<br>
<img width="409" height="381" alt="Ovals_Overlap" src="https://github.com/user-attachments/assets/368ae2f6-daa8-48d8-af05-a9fc4501f4b3" />
<br>
For some twisty little tunnels, try a Cellular Automata algorithm (random noise starting pattern, grown with rules like the game of Life.
<br>
<img width="401" height="384" alt="Celular_Automata" src="https://github.com/user-attachments/assets/66b32778-c33f-422b-aa9d-a3f6b2aedc5d" />
<br>
Then add a low frequency Perlin Noise to the Cellular Automata algorithm.  Connect these bigger rooms with Bezier Curve corridors.
<br>
<img width="405" height="381" alt="Celular_Automata_Perlin" src="https://github.com/user-attachments/assets/f8c33f6c-9bac-44ce-9f24-91aef4b14450" />
<br>
Give the user lots of options to tweak the settings of all these algorithms and a large variety of dungeon styles can be created.
<br>
<img width="885" height="799" alt="Unity_Settings" src="https://github.com/user-attachments/assets/eae93658-3abe-461f-90d9-f9e396ba0c0e" />
<br>
