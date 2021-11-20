using SQLite4Unity3d.Attributes;

namespace Example.Scripts
{
    public class Person
    {
        [PrimaryKey] [AutoIncrement] public int Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public int Age { get; set; }

        public override string ToString()
        {
            return $"[Person: Id={Id}, Name={Name},  Surname={Surname}, Age={Age}]";
        }
    }
}