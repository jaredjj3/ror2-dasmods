## How to add a Command

To add a command:

1. Open `Command.cs`
2. Create a new subclass that inherits from the`ICommand` interface
3. Add a command instance by name to COMMANDS_BY_NAME - the name cannot have any spaces
4. The command will be callable in game by typing /command arg1 arg2 arg3