namespace VESCO.Timeline
{
    public abstract class Track
    {
        public string Name { get; set; }

        protected Track(string name)
        {
            Name = name;
        }
    }
}
