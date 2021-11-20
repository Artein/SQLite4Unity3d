using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SQLite4Unity3d;
using UnityEngine;
using UnityEngine.Assertions;

#if !UNITY_EDITOR
using System.IO;
#endif

namespace Example.Scripts
{
    public class DataService
    {
        private readonly SQLiteConnection _connection;

        public DataService(string databaseName)
        {
#if UNITY_EDITOR
            var dbPath = $@"Assets/StreamingAssets/{databaseName}";
#else
            var dbPersistentDataPath = $"{Application.persistentDataPath}/{databaseName}";

            if (!File.Exists(dbPersistentDataPath)) // check if file exists in Application.persistentDataPath
            {
                Debug.Log("Database not in Persistent path");
                // if it doesn't -> open StreamingAssets directory and load the db ->

#if UNITY_ANDROID
                // this is the path to your StreamingAssets in android
                var loadDb = new WWW("jar:file://" + Application.dataPath + "!/assets/" + databaseName);
                // TODO: place a timer and error check fot this while()
                while (!loadDb.isDone) { }
                File.WriteAllBytes(dbPersistentDataPath, loadDb.bytes);
#elif UNITY_IOS
                // this is the path to your StreamingAssets in iOS
                var loadDb = Application.dataPath + "/Raw/" + databaseName;
                File.Copy(loadDb, dbPersistentDataPath);
#elif UNITY_STANDALONE_OSX
                // this is the path to your StreamingAssets in iOS
		        var loadDb = Application.dataPath + "/Resources/Data/StreamingAssets/" + databaseName;
		        File.Copy(loadDb, dbPersistentDataPath);
#else
                throw new NotImplementedException("Unknown platform");
#endif

                Debug.Log($"Database '{databaseName}' was written");
            }

            var dbPath = dbPersistentDataPath;
#endif
            
            _connection = new SQLiteConnection(dbPath, SQLite3.EOpenFlags.ReadWrite | SQLite3.EOpenFlags.Create);
            Debug.Log("Final database path: " + dbPath);
        }

        public void CreateDatabase(bool dropExisting = true)
        {
            if (dropExisting)
            {
                _connection.DropTable<Person>();
            }
            
            _connection.CreateTable<Person>();
        }

        public IEnumerable<Person> GetAllPersons()
        {
            return _connection.Table<Person>();
        }

        public IEnumerable<Person> GetPersonsWithName(string name)
        {
            Assert.IsFalse(string.IsNullOrEmpty(name));
            return _connection.Table<Person>().Where(person => person.Name == name);
        }

        public Person GetPersonWithName(string name)
        {
            var persons = GetPersonsWithName(name);
            return persons.FirstOrDefault(); // TODO: Is this faster than to call FirstOrDefault() on Table<>()???
        }

        public void Insert([NotNull] Person person)
        {
            _connection.Insert(person);
        }

        public void Insert(IEnumerable<Person> persons)
        {
            _connection.InsertAll(persons);
        }

        public static Person CreatePerson(string name, string surname, int age)
        {
            return new Person
            {
                Name = name,
                Surname = surname,
                Age = age,
            };
        }
    }
}