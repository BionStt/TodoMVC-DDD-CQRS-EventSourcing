# Instructions

**Linux Only**

One docker image we require to run (eventstore) doesn't have a windows container - so run `docker-compose` on linux only for now!

**Start**

```
docker-compose -f docker-compose.yml -f docker-compose.override.yml build
docker-compose -f docker-compose.yml -f docker-compose.override.yml up
```

**Note**
The web frontend is configured to talk to the api server via `http://localhost:8080` in `docker-compose.override.yml`
If you are running `docker-compose up` on another machine you need to change that address to the remote machines ip.
Example: 
```
frontend:
    build:
      args:
        API_SERVER: http://10.0.0.200:8080
```

# Source Code!

Being this project has such a small domain context there are only a couple source files which contain real logic.  Other files are helpers, extensions, or setup.  

### Important backend files:

* [Domain Command Handler](src/Domain/Todo/Handler.cs)
* [Domain Todo Aggregate](src/Domain/Todo/Todo.cs)
* [Read Model Projector](src/Application/Todo/Handler.cs)
* [Web Request Handler](src/Presentation/Service.cs)


### What is EventSourcing?

EventSourcing is a process of representing domain objects (Orders, Invoices, Accounts, etc) as a series of separate events.

Your application ends up being 1 long audit log which records every state-changing event that occurs.  The advantage of this approach is other processes can read this event log and generate models that contain only the information the process cares about.  There is also additional information available that other services perhaps don't record themselves.

Imagine a shoppign cart which fills with items to buy.  The warehouse only cares about the final order of the stuff the customer actually agreed to purchase -

![EventSourcing](img/eventsourcing.png)

but the marketing team might care more about the items the customer removed from their cart **without** buying.  

Using eventsourcing correctly you can generate models which contain both sets of information to satisfy both departments with only 1 set of data.

### What is CQRS

CQRS stands for **Command and Query Responsibility Segregation**

![CQRS](img/cqrs-logical.svg)

In a nut shell - commands are everything that want to change the application state.  Queries are anything that want to read application state.  **There is no overlap**

Commands do not return any data other than if they were *Accepted* or *Rejected*. Accepted meaning the change was saved and read models will be updated.  Rejected meaning the command failed validation or was not valid to be run at this time.  (One example would be trying to invoice a sales order twice)



### Good reads

* [Microsoft's CQRS architecture guide](https://docs.microsoft.com/en-us/azure/architecture/guide/architecture-styles/cqrs)
* [Microsoft's eventsourcing architecture guide](https://docs.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)

### EventStore Management

{host}:2113

### RabbitMq Management

{host}:15672
