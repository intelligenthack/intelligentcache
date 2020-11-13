# Architecture

A cache is a component that stores previously computed values for a period of time, so that the next time the value is needed, the computation can be avoided. The premise is that it is cheaper to look-up a value from the cache than computing that value.

This library implements a cache with multiple hierarchical levels. It means that whenever a lookup is performed, the first level is checked. If a value is found (hit), that value is returned and the second level is not contacted. If the value is not found in the first level (miss), the second level is checked ans so on, recursively.

![A sequence diagram showing three interactions between the application and two cache levels. In the first one, the application requests a key which is found in the first level and returned immediately. In that case the second level isn't contacted at all. In the second interaction, the application requests a key which is not found on the first level, but is found on the second level. The value is stored in the first level and returned to the application. The last interaction shows the application requesting a key that is not found in either cache levels. A new value is then computed, which is stored into both levels.](doc/levels.drawio.svg)

Although the architecture supports an arbitrary number of levels, the predefined configuration is to have an in-process, in-memory cache as the first level, and a shared redis instance as the second level.

## Cache invalidation

The library implements a cache invalidation mechanism that can be used to force the cache to compute the value the next time it is requested. This is useful when the underlying data is modified and we want to make sure that the changes are immediately visible.
Cache invalidation requires synchronization between all the instances of the application, to ensure that their local memory cache is invalidated as well. This synchronization is implemented through Redis' pub/sub mechanism, by broadcasting an invalidation message to all instances.

## Consistency considerations

The cache makes no consistency guarantees. Two instances of the application might contain different values for a given key. The Redis level reduces the likelihood of inconsistencies since it serves as a shared source for all instances of the application.

There are no consistency guarantees between different instances of the application. It is possible for two instances of the application to have different versions of the same data in cache, as illustrated in the following diagram.

![A temporal diagram that shows how different instances of the application can have an inconsistent view of the same data. Instance A requests a key which is found in the Redis level and stored in the in-memory level. Later, that value expires from Redis. When another instance of the application requests the same key, a new value is computed and stored in that instance's memory level. Until the old value expires from the first application instance's memory level, that instance still sees the old value.](doc/consistency.drawio.svg)

In order to minimize inconsistency window, after the Redis cache computes a value, it invalidates all other instances so that the inconsistency window is shortened. The following diagram shows this process.

![The same temporal diagram as before, with the difference that after the Redis level computes a new value, it invalidates the first application instance's cache, therefore reducing the time window during which the different instances are inconsistent.](doc/consistency-improved.drawio.svg)
