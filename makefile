comp=mcs
run=mono

chat.exe: main.cs http.cs
	$(comp) -out:chat.exe main.cs http.cs

.PHONY: run
run: chat.exe
	$(run) chat.exe

.PHONY: clean
clean:
	rm -f *.exe
