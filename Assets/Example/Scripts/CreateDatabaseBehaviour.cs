using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Example.Scripts
{
    public class CreateDatabaseBehaviour : MonoBehaviour
    {
        [SerializeField] private string _databaseName = "tempDatabase.db";
        [SerializeField] private Text _debugText;

        private void Start()
        {
            Assert.IsTrue(_databaseName.Length > 3);
            Assert.IsTrue(_databaseName.EndsWith(".db"));
            var dataService = new DataService(_databaseName);
            dataService.CreateDatabase();
            
            dataService.Insert(new[]
            {
                DataService.CreatePerson(name: "Tom", surname: "Perez", age: 56),
                DataService.CreatePerson(name: "Fred", surname: "Arthurson", age: 16),
                DataService.CreatePerson(name: "John", surname: "Doe", age: 25),
                DataService.CreatePerson(name: "Roberto", surname: "Huertas", age: 37),
            });

            var allPersons = dataService.GetAllPersons();
            ToConsole(allPersons);
            
            ToConsole("Searching for Roberto ...");
            var personsNamedRoberto = dataService.GetPersonsWithName("Roberto");
            ToConsole(personsNamedRoberto);
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            var dbPath = $"Assets/StreamingAssets/{_databaseName}";
            AssetDatabase.DeleteAsset(dbPath);
#endif
        }

        private void ToConsole(IEnumerable<Person> persons)
        {
            foreach (var person in persons)
            {
                ToConsole(person.ToString());
            }
        }

        private void ToConsole(string msg)
        {
            _debugText.text += System.Environment.NewLine + msg;
            Debug.Log(msg);
        }
    }
}