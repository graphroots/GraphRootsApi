# GraphRoots consortium

## Who

We are GraphRoots, a consortium of companies, institutions, and individuals interested in pushing the boundaries of computational design. We met through the worldwide community of Grasshopper and Rhino enthusiasts. While we have diverse nationalities, backgrounds, ages, and locations, we share interests and passions that unite us:

* We use computational design and parametric modeling in our everyday lives, whether for professional, educational, or other purposes.
* We create digital tools.
* We enjoy challenging the status quo and shaping the future.

## Why

Besides our shared interests and passions, we also share some frustrations and ideas on the future. We figured that collaborating on them would benefit not just us but a broader global audience.

## What

We miss modern possibilities to collaborate on parametric models.

There are some tools that address versioning of parametric models. We are not satisfied with the current approaches and believe there’s room to do much better.

Currently there is no open standard for storing parametric models. Among other issues, this makes it difficult to access and reuse the knowledge buried in years of parametric model files. This is especially important for graph machine learning and agentic parametric modeling workflows.

There are no open cloud-native solvers for the types of parametric models we are interested in. Such solvers could be distributed among different machines, CAD kernels, and environments, while also supporting parallel computation depending on the graph structure.

The extension mechanisms for existing parametric modelers have strong limitations (the classic plugin and dependency mess).

## Where do we start?

As a first step, we aim to define an open standard for representing computational graphs and related data. We believe this is a solid foundation since it sits at the core of the challenges we mentioned above.

This standard includes a storage layer based on a graph database, along with a GraphQL API for interacting with the data. Both the API and the storage layer are designed to be easily extensible. Alongside the specification, we are building a reference implementation.

The current slice is that open standard, the Neo4j storage layer, the GraphQL API, and a Grasshopper parser that writes snapshots through GraphQL. See [README.md](README.md) for the reference implementation in this repository.

To get things started, we are also developing client applications:

* A parser for Grasshopper models that writes snapshots through the GraphQL API (in this repository)
* An explorer for visualizing the imported data (planned; not in this tree)

The functionality of these client applications will be inspired by the [Spaghettarium hack](https://github.com/romtecmax/Spaghettarium).

## Where will this go?

Hopefully far! We hope to attract many collaborators and early users, learn from them, and keep iterating from there.

## How do we collaborate

We meet biweekly to coordinate, collect feedback, and welcome new collaborators. Appointment schedule [here](https://calendar.google.com/calendar/appointments/schedules/AcZssZ3MHGFxNyo_51N_Q2cCg3OST3vNfHb87aGz7R2pIgCKnjQEsbsvXZU7n4IVvMkQaVZSOSTw_2Xz).

## Notes, references

### AECTech Hackathons

GraphHop hack: https://github.com/graphhop

GraphHop2 hack: https://github.com/ZMPeterZhang/GraphHop2

Spaghettarium hack: https://github.com/romtecmax/Spaghettarium

### Presentations

Presentation by Alex Schiftner at Rhino Developer Meeting Stockholm, 10th of October 2025: https://snabela.github.io/.github/assets/251010_PechaKucha_Rhino_Dev_Meeting_Final.pdf
