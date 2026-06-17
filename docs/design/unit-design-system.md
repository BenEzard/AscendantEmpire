# Unit Design System

**Terminology specific to this page:**

* Cost - a cost is both a type and a quantity of `Resource`.

* 

## Unit Components

* As the Player gains certain technological advances, they unlock `Unit Components`.

* Once all Unit Components are unlocked, a `Unit Template` is unlocked.
  
  * <span style="color:orange;">TODO: The player will need to be notified that this is available</span>
  
  * <span style="color:orange;">TODO: Need to analyse existing models for obsoleting</span>

### Properties of a Unit Component

Unit Components should have the following properties:

* A unique  Id

* A unique code

* A name

* a component type: body, weapon, engine, armour, equipment, radar, system, fitting, training, special

* a purchase cost - 1 or more resources required to buy it

* Domain

* 

* maintenance cost - 1 or more resources
  
  * combat cost - ammunition 



#### Body Components

* Every `Unit Template` has exactly 1 body component.

* A Body component has these properties:
  
  * Domain
  
  * upkeep costs (per turn)
    
    * sustenance cost (0 or more resources) required to maintain health
    
    - fuel cost (0 or more resources) - maintain movement
  
  - Campaign Movement Points
  
  - Battlefield Movement Points
  
  - 1 or more `Template Slots` where other types of Unit Components can be fitted.
  
  - *May* (optional) have offensive or defensive values
  
  - Minimum strength - minimum number of individuals in a Unit
  
  - Maximum strength - maximum number of individuals in a Unit


