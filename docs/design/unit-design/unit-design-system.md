# Unit Design System

## Concepts & In-Game Considerations





## *Unsorted Notes*





**Terminology specific to this page:**

* Cost - a cost is both a type and a quantity of `Resource`.

* A `Template Slot` is a place where a `Unit Component` can be plugged in to a Body Component. It has the following properties:
  
  * Visible - y/n
  
  * Locked - y/n
  
  * X/Y (where it sits on the UI)
  
  * 1 or more Unit Component acceptance rules (e.g. must be of type weapon, must have a weight no greater than X, must be a subtype of Y)

## Unit Components

* As the Player gains certain technological advances, they unlock `Unit Components`.

* Once all Unit Components are unlocked, a `Unit Template` is unlocked.
  
  * <span style="color:orange;">TODO (later): The player will need to be notified that this is available</span>
  
  * <span style="color:orange;">TODO (later): Need to analyse existing models for obsoleting</span>

### Properties of a Unit Component

Unit Components should have the following properties:

* A unique Id

* A unique code, non null

* A display name, non null

* A description, null

* A Component Type: body, weapon, engine, armour, equipment, radar, system, fitting, training, special

* (TODO LATER) A purchase cost - 1 or more resources required to buy it

* (TODO LATER) A maintenance cost - 1 or more resources 

* 0 or more bonuses/buff tags

* A Detection value (how well they detect other Units) — nullable, fixed-point (Scale 1,000)

* A Visibility value (how easily they are seen) — nullable, fixed-point (Scale 1,000)

#### Body Components

* Every `Unit Template` has exactly 1 body component.

* A Body component has these properties:
  
  * Movement Domain - the Domain Type the Unit moves in: land, sea, air, near-Earth orbit

* (TODO LATER) upkeep costs (per turn)
  
  * sustenance cost (0 or more resources) required to maintain health
  - fuel cost (0 or more resources) - maintain movement
- Campaign Movement Points

- Battlefield Movement Points

- 1 or more `Template Slots` where other types of Unit Components can be fitted.

- (TODO LATER) *May* (optional) have offensive or defensive values

- Minimum strength - minimum number of individuals in a Unit

- Maximum strength - maximum number of individuals in a Unit

- 

- Different components will have different properties
