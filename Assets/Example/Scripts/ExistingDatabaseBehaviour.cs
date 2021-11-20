using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Example.Scripts
{
    public class ExistingDatabaseBehaviour : MonoBehaviour
    {
        [SerializeField] private Text _debugText;

        private void Start()
        {
            var dataService = new DataService("existing.db");
            var allPersons = dataService.GetAllPersons();
            ToConsole(allPersons);

            allPersons = dataService.GetPersonsWithName("Roberto");
            ToConsole("Searching for Roberto ...");
            ToConsole(allPersons);

            dataService.Insert(DataService.CreatePerson(name: "Johnny", surname: "Mnemonic", age: 21));
            ToConsole("New person has been created and inserted");
            
            var person = dataService.GetPersonWithName("Johnny");
            ToConsole(person.ToString());
        }

        private void ToConsole(IEnumerable<Person> people)
        {
            foreach (var person in people)
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