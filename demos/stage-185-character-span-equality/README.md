# Stage 185 · Character span equality

Compare the backing reference, element offset and length of readonly and writable
character spans. Default and zero-address empty native spans share the null
anchor. An empty span with a managed owner or nonzero native address retains
its anchor.

Run `npm run demo -- stage-185-character-span-equality`. The same compiled .NET
Standard 2.1 fixture compares both operators, managed and native aliases, empty
views and the original order of side effects in the Windows CLR, Node and Edge.
